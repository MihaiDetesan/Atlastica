import { polygonPartAreasKm2, totalAreaKm2 } from "./geoArea";
import type { CountryFacts, CountryFeature } from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

let countriesPromise: Promise<CountryFeature[]> | null = null;

export function fetchCountries(): Promise<CountryFeature[]> {
  countriesPromise ??= fetch(`${API_BASE}/api/countries`).then((res) => {
    if (!res.ok) throw new Error(`Failed to fetch countries: ${res.status}`);
    return res.json();
  });
  return countriesPromise;
}

export function fetchCountryFacts(isoCode: string): Promise<CountryFacts> {
  return fetch(`${API_BASE}/api/countries/${isoCode}/facts`).then((res) => {
    if (!res.ok) throw new Error(`Failed to fetch facts for ${isoCode}: ${res.status}`);
    return res.json();
  });
}

export function countriesToFeatureCollection(countries: CountryFeature[]): GeoJSON.FeatureCollection {
  return {
    type: "FeatureCollection",
    features: countries.map((c) => ({
      type: "Feature",
      geometry: c.geometry,
      properties: { isoCode: c.isoCode, name: c.name },
    })),
  };
}

/** Picks the single largest polygon part of a Polygon/MultiPolygon, so a country made of many
 * islands (e.g. an archipelago) still gets exactly one label/flag placement, on its mainland. */
function largestPart(geometry: GeoJSON.Geometry): GeoJSON.Polygon | null {
  if (geometry.type === "Polygon") return geometry;
  if (geometry.type !== "MultiPolygon") return null;
  const areas = polygonPartAreasKm2(geometry);
  let maxIndex = 0;
  for (let i = 1; i < areas.length; i++) {
    if (areas[i] > areas[maxIndex]) maxIndex = i;
  }
  return { type: "Polygon", coordinates: geometry.coordinates[maxIndex] };
}

/** One Polygon feature per country (its largest part), carrying total country area for size
 * scaling - feeds a dedicated label source so each country's name/flag renders exactly once,
 * instead of once per polygon part the way a MultiPolygon-backed symbol layer otherwise would. */
export function countriesToLabelFeatureCollection(countries: CountryFeature[]): GeoJSON.FeatureCollection {
  return {
    type: "FeatureCollection",
    features: countries.flatMap((c) => {
      const part = largestPart(c.geometry);
      if (!part) return [];
      return [
        {
          type: "Feature" as const,
          geometry: part,
          properties: { isoCode: c.isoCode, name: c.name, areaKm2: totalAreaKm2(c.geometry) },
        },
      ];
    }),
  };
}
