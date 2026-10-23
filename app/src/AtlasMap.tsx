import {
  type ExpressionSpecification,
  type GeoJSONSource,
  Map as MapLibreMap,
  type MapLayerMouseEvent,
  Popup,
} from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { useEffect, useRef, useState } from "react";
import { colorCountries } from "./adjacency";
import { BOUNDARY_PALETTE } from "./colors";
import { countriesToFeatureCollection, countriesToLabelFeatureCollection, fetchCountries } from "./data";
import type { CountryFeature } from "./types";

const BASE_STYLE = "https://demotiles.maplibre.org/style.json";

const BOUNDARY_FILL_COLOR = [
  "match",
  ["get", "colorIndex"],
  ...BOUNDARY_PALETTE.flatMap((color, i): [number, string] => [i, color]),
  "#999999",
] as unknown as ExpressionSpecification;

/** Assigns each country a color such that no two touching countries share one (see adjacency.ts). */
function withColoring(fc: GeoJSON.FeatureCollection): GeoJSON.FeatureCollection {
  const colorOf = colorCountries(fc.features);
  return {
    ...fc,
    features: fc.features.map((f) => ({
      ...f,
      properties: {
        ...f.properties,
        colorIndex: colorOf.get(String(f.properties?.isoCode ?? "")) ?? 0,
      },
    })),
  };
}

/** Scales each country's name/flag by its real area (sqrt'd, so it tracks linear size rather
 * than raw area) so Russia's label reads bigger than Luxembourg's rather than all being
 * identical regardless of how much territory they actually fit into. */
function withLabelSizing(fc: GeoJSON.FeatureCollection): GeoJSON.FeatureCollection {
  const sqrtAreas = fc.features.map((f) => Math.sqrt(Number(f.properties?.areaKm2 ?? 0)));
  const min = Math.min(...sqrtAreas);
  const max = Math.max(...sqrtAreas);
  const range = max - min || 1;
  return {
    ...fc,
    features: fc.features.map((f, i) => ({
      ...f,
      properties: {
        ...f.properties,
        labelScale: (sqrtAreas[i] - min) / range,
      },
    })),
  };
}

interface AtlasMapProps {
  onSelectCountry?: (isoCode: string, name: string) => void;
}

export function AtlasMap({ onSelectCountry }: AtlasMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const map = new MapLibreMap({
      container: containerRef.current,
      style: BASE_STYLE,
      center: [15, 20],
      zoom: 2,
      // Avoid tiling adjacent copies of the whole world side by side once
      // zoomed out - only one copy of the world should ever be shown.
      renderWorldCopies: false,
    });
    mapRef.current = map;
    if (import.meta.env.DEV) (window as unknown as { __map?: MapLibreMap }).__map = map;

    // "load" can fire more than once for a single map instance (e.g. a style
    // reload) - use "once" so the source/layer setup below only ever runs once.
    map.once("load", () => {
      // React StrictMode double-invokes this effect in dev (mount → cleanup
      // → mount); "load" fires asynchronously, so a zombie first-mount map
      // that was already removed by cleanup can still fire this callback
      // later and add layers to itself instead of the live map - guard
      // against operating on anything but the current map.
      if (mapRef.current !== map) return;

      // Hide the demo basemap's own country choropleth/labels so they don't
      // compete visually with our own boundary fill/line layers.
      for (const layerId of ["countries-fill", "countries-boundary", "countries-label", "crimea-fill"]) {
        if (map.getLayer(layerId)) map.setLayoutProperty(layerId, "visibility", "none");
      }

      // Prefixed to avoid colliding with the base style's own layers of the
      // same un-prefixed names (it defines "countries-fill", "countries-line"
      // -shaped ids too - see the hide-loop above) - addLayer throws on an id
      // collision, which was silently swallowing everything after it here.
      map.addSource("countries", {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] },
      });
      map.addLayer({
        id: "atlas-countries-fill",
        type: "fill",
        source: "countries",
        paint: { "fill-color": BOUNDARY_FILL_COLOR, "fill-opacity": 0.55 },
      });
      map.addLayer({
        id: "atlas-countries-line",
        type: "line",
        source: "countries",
        paint: { "line-color": "#2b2b2b", "line-width": 1 },
      });
      // A separate point-ish source (one Polygon per country, its largest part
      // only - see countriesToLabelFeatureCollection) feeds the label layer below.
      // Symbol-on-polygon placement in MapLibre places one symbol per polygon
      // *part*, so sourcing straight from "countries" would repeat an
      // archipelago-shaped country's name/flag once per island; this source
      // guarantees exactly one placement per country instead.
      map.addSource("country-labels", {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] },
      });

      // Symbol layers on polygon sources auto-place the label at a sensible
      // interior point (MapLibre computes this per feature) and hide it if
      // it doesn't fit at the current zoom - small countries' names appear
      // once zoomed in enough rather than floating outside their territory.
      // Flag images are loaded lazily per ISO code via "styleimagemissing"
      // below, rather than pre-fetched for every country up front.
      map.on("styleimagemissing", (e) => {
        const id = e.id;
        if (!id.startsWith("flag-")) return;
        const isoCode = id.slice("flag-".length);
        map
          .loadImage(`https://flagcdn.com/24x18/${isoCode.toLowerCase()}.png`)
          .then(({ data }) => {
            if (!map.hasImage(id)) map.addImage(id, data);
          })
          .catch(() => {
            // No flag available for this code - register a transparent 1x1
            // placeholder so MapLibre stops re-requesting it every frame.
            if (!map.hasImage(id)) map.addImage(id, { width: 1, height: 1, data: new Uint8Array(4) });
          });
      });

      map.addLayer({
        id: "atlas-countries-label",
        type: "symbol",
        source: "country-labels",
        layout: {
          "icon-image": ["concat", "flag-", ["get", "isoCode"]],
          "icon-size": ["interpolate", ["linear"], ["get", "labelScale"], 0, 0.35, 1, 1.1],
          "icon-anchor": "right",
          "icon-offset": [-4, 0],
          "icon-allow-overlap": false,
          "text-field": ["get", "name"],
          "text-font": ["Noto Sans Regular"],
          "text-size": ["interpolate", ["linear"], ["get", "labelScale"], 0, 8, 1, 20],
          "text-anchor": "left",
          "text-offset": [0.3, 0],
        },
        paint: {
          "text-color": "#ffffff",
          "text-halo-color": "#000000",
          "text-halo-width": 1.2,
        },
      });

      // Added last (after fill/line/label) so the highlighted contour always
      // renders above every other country - and above its own fill/line, on
      // re-click - rather than being z-fought by paint order.
      map.addSource("selected-country", {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] },
      });
      map.addLayer({
        id: "atlas-selected-line",
        type: "line",
        source: "selected-country",
        paint: { "line-color": "#ffcc00", "line-width": 3 },
      });

      map.on("click", "atlas-countries-fill", (e: MapLayerMouseEvent) => {
        const feature = e.features?.[0];
        const props = feature?.properties;
        if (!feature || !props) return;
        const contentEl = document.createElement("div");
        contentEl.style.display = "flex";
        contentEl.style.alignItems = "center";
        contentEl.style.gap = "6px";
        const flagEl = document.createElement("img");
        flagEl.src = `https://flagcdn.com/24x18/${String(props.isoCode).toLowerCase()}.png`;
        flagEl.width = 24;
        flagEl.height = 18;
        flagEl.alt = "";
        flagEl.style.borderRadius = "2px";
        flagEl.style.flexShrink = "0";
        const nameEl = document.createElement("strong");
        nameEl.textContent = props.name;
        contentEl.append(flagEl, nameEl);
        new Popup().setLngLat(e.lngLat).setDOMContent(contentEl).addTo(map);
        onSelectCountry?.(props.isoCode, props.name);
        const source = map.getSource("selected-country") as GeoJSONSource | undefined;
        // e.features[0] is a MapGeoJSONFeature, not a plain GeoJSON Feature - it
        // carries internal non-serializable fields that make setData's worker
        // postMessage silently fail ("can't serialize object of unregistered
        // class"), so re-wrap it as a plain Feature first.
        source?.setData({
          type: "FeatureCollection",
          features: [{ type: "Feature", geometry: feature.geometry, properties: feature.properties }],
        });
      });
      map.on("mouseenter", "atlas-countries-fill", () => (map.getCanvas().style.cursor = "pointer"));
      map.on("mouseleave", "atlas-countries-fill", () => (map.getCanvas().style.cursor = ""));

      fetchCountries()
        .then((countries: CountryFeature[]) => {
          if (mapRef.current !== map) return;
          const source = map.getSource("countries") as GeoJSONSource | undefined;
          source?.setData(withColoring(countriesToFeatureCollection(countries)));
          const labelSource = map.getSource("country-labels") as GeoJSONSource | undefined;
          labelSource?.setData(withLabelSizing(countriesToLabelFeatureCollection(countries)));
        })
        .catch((err: unknown) => setError(err instanceof Error ? err.message : String(err)));
    });

    return () => {
      map.remove();
      mapRef.current = null;
    };
  }, [onSelectCountry]);

  return (
    <>
      <div ref={containerRef} className="map-container" style={{ position: "absolute", inset: 0 }} />
      {error && (
        <div style={{ position: "absolute", top: 12, left: 12, background: "#e34948", color: "#fff", padding: "6px 12px", borderRadius: 4 }}>
          Failed to load countries: {error}
        </div>
      )}
    </>
  );
}
