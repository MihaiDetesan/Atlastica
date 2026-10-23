const EARTH_RADIUS_M = 6378137;

function ringAreaRad(ring: GeoJSON.Position[]): number {
  // Spherical excess formula (the same approach turf/area uses internally) -
  // sums the signed area contribution of each edge over the sphere rather
  // than treating lon/lat as flat Cartesian, so high-latitude countries
  // aren't over/under-weighted relative to equatorial ones.
  let total = 0;
  const n = ring.length;
  for (let i = 0; i < n; i++) {
    const [lon1] = ring[i];
    const [, lat2] = ring[(i + 1) % n];
    const [lon3] = ring[(i + 2) % n];
    total += (toRad(lon3) - toRad(lon1)) * Math.sin(toRad(lat2));
  }
  return (total * EARTH_RADIUS_M * EARTH_RADIUS_M) / 2;
}

function toRad(deg: number): number {
  return (deg * Math.PI) / 180;
}

function polygonArea(coordinates: GeoJSON.Position[][]): number {
  if (coordinates.length === 0) return 0;
  // First ring is the outer boundary; subtract holes.
  let area = Math.abs(ringAreaRad(coordinates[0]));
  for (let i = 1; i < coordinates.length; i++) {
    area -= Math.abs(ringAreaRad(coordinates[i]));
  }
  return area;
}

/** Area in square kilometers of each separate polygon part (rings[0] = outer boundary) of a Polygon/MultiPolygon. */
export function polygonPartAreasKm2(geometry: GeoJSON.Geometry): number[] {
  if (geometry.type === "Polygon") {
    return [polygonArea(geometry.coordinates) / 1_000_000];
  }
  if (geometry.type === "MultiPolygon") {
    return geometry.coordinates.map((part) => polygonArea(part) / 1_000_000);
  }
  return [];
}

/** Total area in square kilometers across all parts of a Polygon/MultiPolygon. */
export function totalAreaKm2(geometry: GeoJSON.Geometry): number {
  return polygonPartAreasKm2(geometry).reduce((sum, a) => sum + a, 0);
}
