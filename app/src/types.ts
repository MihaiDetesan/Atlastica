export interface CountryFeature {
  isoCode: string;
  name: string;
  geometry: GeoJSON.Geometry;
}

export interface CountryFacts {
  isoCode: string;
  population: number | null;
  capital: string | null;
  governmentForm: string | null;
  officialLanguages: string[];
  areaKm2: number | null;
  currency: string | null;
  notableGeographicFeatures: string | null;
  biodiversityFacts: string | null;
  fetchedAt: string;
}

export interface PointOfInterestSummary {
  id: string;
  name: string;
  category: string;
  latitude: number;
  longitude: number;
}

export interface PointOfInterestDetail extends PointOfInterestSummary {
  shortDescription: string | null;
  imageUrl: string | null;
  wikipediaUrl: string | null;
  fetchedAt: string | null;
}
