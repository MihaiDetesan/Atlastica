// 7-color categorical palette for distinguishing neighboring country
// polygons (see adjacency.ts - colors are assigned by actual polygon
// adjacency, not by this list's order). Subset of Historica's validated
// palette (dataviz skill, scripts/validate_palette.js --mode light): passes
// lightness/chroma/CVD separation for the adjacent-pairs case relevant here
// (a polygon only touches a handful of neighbors, not all 7 at once) -
// dropping one color from the validated 8-hue set keeps that guarantee.
export const BOUNDARY_PALETTE = [
  "#2a78d6", // blue
  "#eb6834", // orange
  "#1baf7a", // aqua
  "#eda100", // yellow
  "#e87ba4", // magenta
  "#008300", // green
  "#4a3aa7", // violet
];
