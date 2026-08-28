# Atlastica

World map with per-country facts and points-of-interest (POI), backed by a Postgres
database. Unlike [Historica](../Historica), which bakes everything offline into static
files served directly by the frontend, Atlastica has a real backend API — but the
*expensive* part (Wikidata/Wikipedia fetching + local NLP extraction) also runs
offline, as a separate console tool, not on the API request path. The API only ever
reads what that tool already wrote to Postgres.

## Architecture

```
Natural Earth (boundaries, seeded once, resynced ~180d) ──────────────────┐
                                                                            ├─► Postgres ─► ASP.NET Core API (read-only) ─► React/MapLibre frontend
Wikidata + Wikipedia + local NLP (Atlastica.FactsExtractor, run offline) ──┘
```

- `app/` — React + TypeScript + Vite + MapLibre GL JS frontend. Map component follows
  the same imperative-MapLibre pattern as Historica's `app/src/HistoryMap.tsx`: a
  GeoJSON source + fill/line layers for country boundaries, a second GeoJSON source +
  symbol layer for POI markers, click handlers open detail popups/panels.
- `backend/Atlastica.Api` — ASP.NET Core Web API, controllers + DI wiring. Read-only:
  it queries Postgres and never calls Wikidata/Wikipedia/the NLP model itself.
- `backend/Atlastica.Domain` — entities: `Country`, `CountryFacts`, `PointOfInterest`.
- `backend/Atlastica.Infrastructure` — EF Core `DbContext` (Npgsql), migrations, the
  background boundary-refresh job, and the fetch+NLP building blocks
  (`WikidataCountryFetcher`, `WikipediaFetcher`, `CountryFactsComputer`) - these are
  wired into the API's DI container (`AddAtlasticaInfrastructure`) only for boundaries;
  the fact-extraction pieces are registered separately (`AddAtlasticaFactsExtraction`)
  and used only by `Atlastica.FactsExtractor`, so the API process never loads them.
- `backend/Atlastica.Nlp` — wraps a small local model (Phi-3.5-mini-instruct, run
  in-process via ONNX Runtime GenAI) that turns raw Wikipedia extract text into
  structured fields (government description, notable geographic features,
  biodiversity facts, POI short descriptions). No external LLM API calls.
- `backend/Atlastica.FactsExtractor` — offline console tool, the only thing that
  actually talks to Wikidata/Wikipedia/the NLP model:
  - `dotnet run -- extract [--file path] [--only ISO,ISO]` fetches+extracts facts for
    every country and writes them to a JSON file (`backend/data/country-facts.json`
    by default). Resumable — re-running skips countries already in the file, and
    per-country failures are logged and skipped rather than aborting the run.
  - `dotnet run -- import [--file path]` upserts that JSON file into `country_facts`.
  - This two-step (file, then import) is deliberate: the intermediate JSON is
    reviewable/diffable before it ever touches the live DB.
- `backend/models/` — the local model weights (~2.6GB, gitignored; fetch with
  `backend/models/download.sh`).

## Data model (Postgres)

- `countries(iso_code PK, name, boundary_geojson jsonb, boundary_fetched_at)`
- `country_facts(iso_code PK/FK, population, capital, government_form,
  official_languages[], area_km2, currency, notable_geographic_features,
  biodiversity_facts, source_extract_hash, fetched_at)`
- `points_of_interest(id PK, name, slug, category, latitude, longitude,
  short_description, image_url, wikipedia_url, fetched_at)`

`notable_geographic_features` and `biodiversity_facts` are deliberately opportunistic,
not mandatory - most countries don't have an internationally famous named mountain,
river, or ecosystem, and the extraction prompt is explicit that null is the right
answer far more often than not (a forced "not specified"-style placeholder for every
country was the earlier, wrong behavior). The frontend omits these fields entirely
when null rather than showing a placeholder row.

`boundary_geojson` is stored as `jsonb`, not PostGIS geometry — no spatial queries are
needed (clicks are matched by feature property, same as Historica's boundary layer), so
this stays intentionally simple. Revisit if a "which country contains this point" query
is ever needed.

## Refresh policy

Both boundaries and country facts are refreshed **offline**, not on the request path -
the API is a pure read from Postgres either way, driven by a `fetched_at` column:

- **Boundaries**: refreshed by a background job in the API process
  (`IHostedService` + `PeriodicTimer`, checked daily) if `boundary_fetched_at` is
  older than ~180 days. Political boundaries change rarely and the seed data is
  bundled, so this is cheap to leave running in-process.
- **Country facts**: refreshed by manually re-running `Atlastica.FactsExtractor`
  (`extract` then `import`) - there's no TTL/staleness check in the API. Facts
  extraction is CPU-bound (local model generation, ~100s/country) and not something
  to trigger from a user's click; re-run the tool periodically (e.g. every 40-60
  days) instead.

## POI seed list

POI *existence and coordinates* are hand-curated (mirrors Historica's `manual.json`
pattern — a fixed list is safer than trying to auto-discover "interesting places").
Each entry's *description and image* are expected to go through the same
Wikidata/Wikipedia + NLP pipeline as country facts (via `Atlastica.FactsExtractor`),
not hand-written, once POI support is built (not yet implemented).

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
