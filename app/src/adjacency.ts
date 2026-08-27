import bbox from "@turf/bbox";
import booleanIntersects from "@turf/boolean-intersects";
import { BOUNDARY_PALETTE } from "./colors";

interface BBox {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

function bboxesOverlap(a: BBox, b: BBox): boolean {
  return a.minX <= b.maxX && a.maxX >= b.minX && a.minY <= b.maxY && a.maxY >= b.minY;
}

/**
 * Two countries are neighbors if their polygons touch or overlap. A bbox
 * prefilter avoids running the (relatively expensive) exact intersection
 * test on the ~15k pairs among ~180 countries that obviously can't touch.
 */
export function computeAdjacency(features: GeoJSON.Feature[]): Map<string, Set<string>> {
  const ids = features.map((f) => String(f.properties?.isoCode ?? ""));
  const bboxes: BBox[] = features.map((f) => {
    const [minX, minY, maxX, maxY] = bbox(f);
    return { minX, minY, maxX, maxY };
  });

  const adjacency = new Map<string, Set<string>>(ids.map((id) => [id, new Set<string>()]));

  for (let i = 0; i < features.length; i++) {
    for (let j = i + 1; j < features.length; j++) {
      if (!bboxesOverlap(bboxes[i], bboxes[j])) continue;
      if (!booleanIntersects(features[i], features[j])) continue;
      adjacency.get(ids[i])?.add(ids[j]);
      adjacency.get(ids[j])?.add(ids[i]);
    }
  }

  return adjacency;
}

/**
 * Greedy Welsh-Powell coloring (highest-degree nodes first) over the
 * adjacency graph, cycling through a fixed-size palette. Real-world country
 * adjacency graphs are near-planar and need only a handful of colors (the
 * four-color theorem caps planar maps at 4), so a 7-color palette leaves
 * comfortable headroom - the fallback below (reuse the color with the
 * fewest same-colored neighbors) only matters if that assumption is ever
 * wrong, so no two touching countries are ever left literally uncolored.
 */
export function colorCountries(features: GeoJSON.Feature[], paletteSize = BOUNDARY_PALETTE.length): Map<string, number> {
  const adjacency = computeAdjacency(features);
  const order = [...adjacency.keys()].sort((a, b) => (adjacency.get(b)?.size ?? 0) - (adjacency.get(a)?.size ?? 0));

  const colorOf = new Map<string, number>();

  for (const id of order) {
    const neighborColors = new Set([...(adjacency.get(id) ?? [])].map((n) => colorOf.get(n)).filter((c) => c !== undefined));

    let chosen = -1;
    for (let c = 0; c < paletteSize; c++) {
      if (!neighborColors.has(c)) {
        chosen = c;
        break;
      }
    }

    if (chosen === -1) {
      // Every palette color is used by some neighbor - pick the one used by
      // the fewest neighbors instead of leaving this node uncolored.
      const counts = new Array(paletteSize).fill(0);
      for (const n of adjacency.get(id) ?? []) {
        const nc = colorOf.get(n);
        if (nc !== undefined) counts[nc]++;
      }
      chosen = counts.indexOf(Math.min(...counts));
    }

    colorOf.set(id, chosen);
  }

  return colorOf;
}
