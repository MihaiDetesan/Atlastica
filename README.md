# Atlastica

An interactive world map: click a country to see population, form of government,
official languages, area, currency, and notable mountains/rivers/biodiversity facts.
Click a point-of-interest marker (Lake Victoria, Mariana Trench, Grand Canyon, ...) to
see a short description and picture.

Sibling project to [Historica](../Historica) (same map-rendering approach), but with a
real backend + database instead of a static offline pipeline, since country/POI facts
are fetched from live public sources and refreshed periodically.

## Stack

- `app/` — React + TypeScript + Vite + MapLibre GL JS
- `backend/` — ASP.NET Core Web API + PostgreSQL (EF Core / Npgsql)

See [CLAUDE.md](./CLAUDE.md) for architecture, data flow, and data source details.

## Running locally

### Database

```bash
# local Postgres via Docker (host port 5433, to avoid clashing with any
# system Postgres already on 5432)
docker run --name atlastica-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=atlastica -p 5433:5432 -d postgres:16
```

### Backend

```bash
cd backend/Atlastica.Api
dotnet ef database update
dotnet run
```

### Frontend

```bash
cd app
npm install
npm run dev
```

## Data & licensing

- Country boundaries: [Natural Earth](https://www.naturalearthdata.com/) (public domain).
- Structured country facts: [REST Countries](https://restcountries.com/).
- Free-text facts, POI descriptions, and images: Wikipedia / Wikimedia Commons
  (CC BY-SA — attribution shown in the UI where this data is displayed).
