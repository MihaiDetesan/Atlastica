import {
  type ExpressionSpecification,
  Map as MapLibreMap,
  type MapLayerMouseEvent,
  Popup,
} from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { useEffect, useRef, useState } from "react";
import { BOUNDARY_PALETTE, hashToColorIndex } from "./colors";
import { countriesToFeatureCollection, fetchCountries } from "./data";
import type { CountryFeature } from "./types";

const BASE_STYLE = "https://demotiles.maplibre.org/style.json";

const BOUNDARY_FILL_COLOR = [
  "match",
  ["get", "colorIndex"],
  ...BOUNDARY_PALETTE.flatMap((color, i): [number, string] => [i, color]),
  "#999999",
] as unknown as ExpressionSpecification;

function withColorIndex(fc: GeoJSON.FeatureCollection): GeoJSON.FeatureCollection {
  return {
    ...fc,
    features: fc.features.map((f) => ({
      ...f,
      properties: {
        ...f.properties,
        colorIndex: hashToColorIndex(String(f.properties?.isoCode ?? ""), BOUNDARY_PALETTE.length),
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

    map.on("load", () => {
      // Hide the demo basemap's own country choropleth/labels so they don't
      // compete visually with our own boundary fill/line layers.
      for (const layerId of ["countries-fill", "countries-boundary", "countries-label", "crimea-fill"]) {
        if (map.getLayer(layerId)) map.setLayoutProperty(layerId, "visibility", "none");
      }

      map.addSource("countries", {
        type: "geojson",
        data: { type: "FeatureCollection", features: [] },
      });
      map.addLayer({
        id: "countries-fill",
        type: "fill",
        source: "countries",
        paint: { "fill-color": BOUNDARY_FILL_COLOR, "fill-opacity": 0.55 },
      });
      map.addLayer({
        id: "countries-line",
        type: "line",
        source: "countries",
        paint: { "line-color": "#2b2b2b", "line-width": 1 },
      });

      map.on("click", "countries-fill", (e: MapLayerMouseEvent) => {
        const props = e.features?.[0]?.properties;
        if (!props) return;
        new Popup().setLngLat(e.lngLat).setHTML(`<strong>${props.name}</strong>`).addTo(map);
        onSelectCountry?.(props.isoCode, props.name);
      });
      map.on("mouseenter", "countries-fill", () => (map.getCanvas().style.cursor = "pointer"));
      map.on("mouseleave", "countries-fill", () => (map.getCanvas().style.cursor = ""));

      fetchCountries()
        .then((countries: CountryFeature[]) => {
          const source = map.getSource("countries");
          if (source && "setData" in source) {
            source.setData(withColorIndex(countriesToFeatureCollection(countries)));
          }
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
