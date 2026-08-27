# Atlastica

World map with per-country facts and points-of-interest (POI), backed by a live
database that's populated and periodically refreshed from public sources — unlike
[Historica](../Historica), which bakes everything offline into static files ahead of
time, Atlastica fetches data on demand and caches it with a staleness TTL.

## Architecture

```
Natural Earth (boundaries, seeded once, resynced ~180d)  ─┐
Wikidata SPARQL (structured facts: population, area, ...)  ├─► Postgres ─► ASP.NET Core API ─► React/MapLibre frontend
Wikipedia + local NLP model (free-text facts, POI, images)─┘
```

- `app/` — React + TypeScript + Vite + MapLibre GL JS frontend. Map component follows
  the same imperative-MapLibre pattern as Historica's `app/src/HistoryMap.tsx`: a
  GeoJSON source + fill/line layers for country boundaries, a second GeoJSON source +
  symbol layer for POI markers, click handlers open detail popups/panels.
- `backend/Atlastica.Api` — ASP.NET Core Web API, controllers + DI wiring.
- `backend/Atlastica.Domain` — entities: `Country`, `CountryFacts`, `PointOfInterest`.
- `backend/Atlastica.Infrastructure` — EF Core `DbContext` (Npgsql), migrations,
  external fetchers (`NaturalEarthBoundaryFetcher`, `WikidataCountryFetcher`,
  `WikipediaFetcher`), and the background boundary-refresh job.
- `backend/Atlastica.Nlp` — wraps a small local model that turns raw Wikipedia extract
  text into structured fields (government description, mountains, rivers, biodiversity
  facts, POI short descriptions). Runs in-process, no external LLM API calls.

## Data model (Postgres)

- `countries(iso_code PK, name, boundary_geojson jsonb, boundary_fetched_at)`
- `country_facts(iso_code PK/FK, population, government_form, official_languages[],
  area_km2, currency, main_mountains, main_rivers, biodiversity_facts,
  source_extract_hash, fetched_at)`
- `points_of_interest(id PK, name, slug, category, latitude, longitude,
  short_description, image_url, wikipedia_url, fetched_at)`

`boundary_geojson` is stored as `jsonb`, not PostGIS geometry — no spatial queries are
needed (clicks are matched by feature property, same as Historica's boundary layer), so
this stays intentionally simple. Revisit if a "which country contains this point" query
is ever needed.

## Caching / refresh policy

Two different policies, both driven by a `fetched_at` timestamp column:

- **Boundaries**: refreshed by a background job (`IHostedService` + `PeriodicTimer`,
  checked daily) if `boundary_fetched_at` is older than ~180 days. Political boundaries
  change rarely; this doesn't need to be on the request path.
- **Country facts & POI**: refreshed **on demand**, on the request path. The shared
  cache-or-refetch helper checks `fetched_at`: if within the configured TTL
  (40–60 days, configurable), serve straight from Postgres; otherwise re-fetch from
  source (Wikidata / Wikipedia), re-run NLP extraction, update the row (and
  `fetched_at`) before responding.

## POI seed list

POI *existence and coordinates* are hand-curated (mirrors Historica's `manual.json`
pattern — a fixed list is safer than trying to auto-discover "interesting places").
Each entry's *description and image* still go through the same Wikipedia + NLP
fetch/cache pipeline as country facts, not hand-written.

## Licensing / attribution

- Natural Earth: public domain, no attribution required.
- Wikidata: CC0, no attribution required.
- Wikipedia / Wikimedia Commons: CC BY-SA — attribution must be shown in the UI
  wherever this content is displayed (country facts panel, POI popups).

## Conventions

- No time/year dimension (Historica has a year slider; Atlastica is a single
  present-day snapshot).
- Env vars (Postgres connection string, TTL settings) live in
  `backend/Atlastica.Api/appsettings.json` / `appsettings.Development.json`, overridable
  via standard ASP.NET Core env var conventions — not hardcoded, unlike Historica's
  pipeline scripts.
