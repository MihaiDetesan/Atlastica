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
import { countriesToFeatureCollection, fetchCountries } from "./data";
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
      // Symbol layers on polygon sources auto-place the label at a sensible
      // interior point (MapLibre computes this per feature) and hide it if
      // it doesn't fit at the current zoom - small countries' names appear
      // once zoomed in enough rather than floating outside their territory.
      map.addLayer({
        id: "atlas-countries-label",
        type: "symbol",
        source: "countries",
        layout: {
          "text-field": ["get", "name"],
          "text-font": ["Noto Sans Regular"],
          "text-size": 11,
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
        const nameEl = document.createElement("strong");
        nameEl.textContent = props.name;
        new Popup().setLngLat(e.lngLat).setDOMContent(nameEl).addTo(map);
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
