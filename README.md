# SunflowerApi

Backend API for the Sunflower application — a chart/data-visualization platform that serves statistical charts (e.g. from ISTAT, Eurostat) with hybrid keyword + semantic search, questionnaires, user events, and feedback collection.

Built with **ASP.NET Core (.NET 9)** and **PostgreSQL** (via Npgsql/EF Core, with `pgvector` for semantic search).

---

## Table of contents

- [Architecture](#architecture)
- [Project structure](#project-structure)
- [Data layer](#data-layer)
- [Authentication](#authentication)
- [Rate limiting](#rate-limiting)
- [Search](#search)
- [API documentation](#api-documentation)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Running](#running)

---

## Architecture

The project follows a layered **Endpoint → Service → Repository → DbContext** pattern using ASP.NET Core Minimal APIs:

```
Endpoints   → HTTP routing, request binding, response shaping
Services    → business logic, validation, orchestration
Repositories→ data access (raw SQL via Npgsql and/or EF Core)
Data        → EF Core DbContexts, one per bounded domain
Models      → EF Core entities / DTOs
Tools       → standalone utilities (parsing, scheduling helpers, etc.)
```

Each domain (Charts, Search, Dictionary, Questionnaire, Data Requests, DB Metadata, User Events, Feedback, Category) has its own interface + implementation pair for both the service and repository layers, registered via dependency injection in `Program.cs`.

## Project structure

```
SunflowerApi/
├── Data/                          # EF Core DbContexts (one per domain)
│   ├── CategoryDbContext.cs
│   ├── ChartDbContext.cs
│   ├── DbMetadataDbContext.cs
│   ├── DictionaryDbContext.cs
│   ├── FeedbackContext.cs
│   ├── QuestionnaireDbContext.cs
│   ├── TranslationDbContext.cs
│   └── UserEventDbContext.cs
│
├── Endpoints/                     # Minimal API route registration
│   ├── DataEndpoints.cs
│   ├── DataRequestEndpoints.cs
│   ├── DbMetadataEndpoints.cs
│   ├── DictionaryEndpoints.cs
│   ├── FeedbackEndpoints.cs
│   ├── QuestionnaireEndpoints.cs
│   ├── SearchEndpoints.cs
│   └── UserEventEndpoints.cs
│
├── Models/                        # Entity / DTO definitions
│   ├── Categories.cs
│   ├── Charts.cs
│   ├── DataRequest.cs
│   ├── DbsMetadata.cs
│   ├── Dictionaries.cs
│   ├── Feedback.cs
│   ├── Questionnaires.cs
│   ├── SearchableAssets.cs
│   └── UserEvents.cs
│
├── Repositories/                  # Data access layer
│   ├── BaseRepository.cs
│   ├── ChartRepository.cs / IChartRepository.cs
│   ├── DataRequestRepository.cs / IDataRequestRepository.cs
│   ├── DbMetadataRepository.cs / IDbMetadataRepository.cs
│   ├── DictionaryRepository.cs / IDictionaryRepository.cs
│   ├── FeedbackRepository.cs / IFeedbackRepository.cs
│   ├── QuestionnaireRepository.cs / IQuestionnaireRepository.cs
│   ├── SearchRepository.cs / ISearchRepository.cs
│   └── UserEventRepository.cs / IUserEventRepository.cs
│
├── Services/                      # Business logic layer
│   ├── ChartService.cs / IChartService.cs
│   ├── DataRequestService.cs / IDataRequestService.cs
│   ├── DbMetadataService.cs / IDbMetadataService.cs
│   ├── DictionaryService.cs / IDictionaryService.cs
│   ├── DynamicQueryService.cs / IDynamicQueryService.cs
│   ├── FeedbackService.cs / IFeedbackService.cs
│   ├── QuestionnaireService.cs / IQuestionnaireService.cs
│   ├── SearchService.cs / ISearchService.cs
│   └── UserEventService.cs / IUserEventService.cs
│
├── Tools/                         # Standalone utilities
│   ├── CodeParser.cs
│   ├── DateSpacing.cs
│   └── UserEventTool.cs
│
├── bin/                            # Build output (git-ignored)
├── obj/                            # Build intermediates (git-ignored)
├── publish/                        # Publish output (git-ignored)
├── Properties/                     # launchSettings.json, etc.
├── .vscode/                        # Editor config (git-ignored)
├── appsettings.json                # Base configuration (tracked)
├── appsettings.Development.json    # Local overrides (git-ignored, has secrets)
├── .gitignore
├── Program.cs                      # App composition root
└── SunflowerApi.sln
```

## Data layer

Each domain has its own `DbContext`, all pointed at the **same PostgreSQL connection string** (`DefaultConnection`), registered via `AddDbContextPool`:

| Context | Purpose |
|---|---|
| `ChartDbContext` | Charts + vector embeddings (`pgvector`, enabled via `UseVector()`) |
| `UserEventsDbContext` | User interaction/analytics events |
| `DbMetadataDbContext` | Metadata about underlying source databases (`public.dbs`, incl. `db_source`) |
| `CategoryDbContext` | Chart categories |
| `TranslationDbContext` | Localized text |
| `DictionaryDbContext` | Dictionary/lookup terms |
| `QuestionnaireDbContext` | Questionnaires |
| `FeedbackDbContext` | User feedback |

> Since all contexts share one connection string, cross-domain joins (e.g. filtering charts by their source database) can be done directly in raw SQL inside a single repository rather than requiring cross-context lookups.

`SearchRepository` uses raw `Npgsql` SQL (rather than EF Core LINQ) to implement hybrid search — see [Search](#search) below.

## Authentication

Authentication is handled via **Clerk**, using JWT Bearer tokens:

- Configured with `AddJwtBearer`, pulling the issuer/authority from `Clerk:Authority` in configuration.
- Clerk's JWKS endpoint (`{Authority}/.well-known/jwks.json`) is used automatically by the middleware to validate token signatures.
- `NameClaimType` is mapped to the `sub` claim.
- Token lifetime validation is **disabled in Development** to ease local testing, but enabled in all other environments.
- A **fallback authorization policy** requires an authenticated user on every endpoint by default; individual endpoints must explicitly opt out with `.AllowAnonymous()`.

Swagger UI is configured with a Bearer auth scheme (paste the raw Clerk JWT, without the `Bearer` prefix) for authenticated testing via `NSwag`.

## Rate limiting

A named fixed-window rate limiter (`"data-request"`) is registered:

- **5 requests / 10 minutes**, no queueing (`QueueLimit = 0`)
- Rejections return **HTTP 429**

Applied selectively via `.RequireRateLimiting("data-request")` on the relevant endpoint(s) (e.g. data request submission), not globally.

**Middleware order matters:** `UseAuthentication` → `UseAuthorization` → `UseRateLimiter`, so identity is established before any policy or limiter is evaluated.

## Search

`POST /chart/searchChart` implements **hybrid search** over charts, combining:

1. **Keyword search** — `ILIKE`-based matching on chart title/description, ranked (exact > prefix > partial title > description match), with special characters escaped to prevent wildcard injection.
2. **Semantic search** — cosine similarity over `pgvector` embeddings (384-dim, matching the `Xenova/multilingual-e5-small` model used by the on-device embedding service), thresholded to exclude weak matches.
3. **Fused mode** — when both a text query and an embedding vector are supplied, results are combined via **Reciprocal Rank Fusion (RRF, k=60)** rather than naively averaging two incomparable score scales.

Supports:
- Optional `category` filter
- Keyset pagination (plain listing) or offset-based pagination (search modes), returned as an opaque `nextCursor`
- `lang` for localized title/description via `charts_text`

> **Recommended indexes** for production scale are documented directly above `SearchRepository` in code (composite index on `charts_text`, an ANN/HNSW index on the embedding column, and a plain index on `charts.id` for keyset browsing). Without them, semantic/fused queries fall back to a full sequential scan — acceptable at the current catalog size, but worth revisiting if the charts table grows substantially.

## API documentation

- **NSwag** generates an OpenAPI document (`AddOpenApiDocument`), exposed via **Swagger UI**.
- Swagger UI and the raw OpenAPI endpoint are enabled **only in Development** (`app.Environment.IsDevelopment()`).
- A Bearer security scheme is wired into the OpenAPI spec so JWTs can be tested directly from the Swagger UI.

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL with the [`pgvector`](https://github.com/pgvector/pgvector) extension enabled (or a compatible CockroachDB setup — see notes in `SearchRepository`)
- A Clerk application (for JWT authentication)

### Configuration

Create `appsettings.Development.json` locally (this file is git-ignored and must **never** be committed):

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Port=5432;Database=...;Username=...;Password=..."
  },
  "Clerk": {
    "Authority": "https://your-clerk-instance.clerk.accounts.dev"
  }
}
```

`appsettings.json` (tracked in git) should contain only non-sensitive defaults/placeholders.

### Running

```bash
dotnet restore
dotnet run --urls "http://0.0.0.0:5013"
```

In Development, browse to `/swagger` for interactive API docs.

### Building

```bash
dotnet build
```

### Publishing

```bash
dotnet publish -c Release -o ./publish
```

---
