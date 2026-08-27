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
