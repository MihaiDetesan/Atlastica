// Categorical palette for distinguishing neighboring country polygons.
// Reused from Historica's validated palette (dataviz skill,
// scripts/validate_palette.js --mode light): passes lightness/chroma/CVD
// separation for the adjacent-pairs case relevant here (a polygon only
// touches a handful of neighbors, not all 8 at once).
export const BOUNDARY_PALETTE = [
  "#2a78d6", // blue
  "#eb6834", // orange
  "#1baf7a", // aqua
  "#eda100", // yellow
  "#e87ba4", // magenta
  "#008300", // green
  "#4a3aa7", // violet
  "#e34948", // red
];

export function hashToColorIndex(key: string, bucketCount: number): number {
  let hash = 0;
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) | 0;
  }
  return Math.abs(hash) % bucketCount;
}
