# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is GOLP

Multi-tenant PWA for amateur sports clubs (MVP: 2v2 padel, beach tennis, basket 2v2, burraco). Players register matches, all 4 participants confirm results, then an ELO-style rating updates. Multi-tenancy is via `circle_id` on every entity — never leak data across circles.

## Commands

### Backend (ASP.NET Core — `src/`)

```powershell
# Run API (http://localhost:5120)
dotnet run --project src/Golp.Api

# All tests
dotnet test src/Golp.Tests

# Single test class
dotnet test src/Golp.Tests --filter "FullyQualifiedName~CircleIntegrationTests"

# Single test method
dotnet test src/Golp.Tests --filter "FullyQualifiedName~Register_ValidData_Returns200WithJwt"

# Add EF migration
dotnet ef migrations add <Name> --project src/Golp.Api

# Apply migrations
dotnet ef database update --project src/Golp.Api
```

### Frontend (Angular 19 — `frontend/golp-app/`)

```powershell
cd frontend/golp-app

# Dev server with API proxy (http://localhost:4200 → http://localhost:5120)
npm start

# Unit tests (Karma/Jasmine)
npm test

# E2E tests (Playwright, requires both servers running)
npm run e2e
npm run e2e:headed   # headed
npm run e2e:ui       # Playwright UI
```

## Architecture

### Backend

`src/Golp.Api` — ASP.NET Core Minimal API, .NET 10, EF Core + SQL Server.

- **`Program.cs`** — wires DI, JWT bearer auth, CORS, then calls `MapXxxEndpoints()`.
- **`Endpoints/`** — one static class per domain (`AuthEndpoints`, `CircleEndpoints`, `MatchEndpoints`). Each exposes a `MapXxxEndpoints(IEndpointRouteBuilder)` extension. Request/response DTOs are `record`s at the bottom of the file.
- **`Data/AppDbContext.cs`** — single EF context; all entity config is in `OnModelCreating`.
- **`Data/Entities/`** — plain entity classes. `CircleMembership` is the join table that also carries per-circle `Rating`.
- **`Services/`** — `JwtService` (token generation/validation), `PasswordResetService`, `DevelopmentEmailService` (console-only, no SMTP in dev), `SportsConfig` (static list of supported sports).
- **`Migrations/`** — EF code-first migrations; never edit generated files.

Match status lifecycle: `pending` → `confirmed` (4/4 confirmations) or `disputed` (any rejection). Only `confirmed` matches affect ratings.

### Frontend

`frontend/golp-app` — Angular 19, standalone components, lazy-loaded routes.

- **`app.config.ts`** — provides `HttpClient` with `authInterceptor` (attaches `Bearer` token from `localStorage` to every request).
- **`app.routes.ts`** — all routes; protected ones use `authGuard`. Lazy-loaded via `loadComponent`.
- **`auth/`** — `AuthService` (JWT store in localStorage), `auth.guard.ts`, `auth.interceptor.ts`, and four page components (login, register, forgot-password, reset-password).
- **`circles/`** — `circle.service.ts` (circle + membership API calls), `match.service.ts` (match API calls), and page components (my-circles, create-circle, browse-circles, record-match).
- **`dashboard/`** — post-login landing.
- **`proxy.conf.json`** — dev proxy forwards `/auth`, `/circles`, `/sports` to `http://localhost:5120`. Add new API path prefixes here when needed.

### Test setup

`Golp.Tests` uses `WebApplicationFactory<Program>` + `EF InMemory` for integration tests. Each test class gets `IntegrationTestFactory` via `IClassFixture<>`. Unit tests (e.g. `JwtServiceTests`) use Moq.

### Key domain rules encoded in the API

- `SportsConfig` is the single source of truth for valid sports and their `point_unit / sets / team_size`. Extend here, not in the DB.
- `CircleMembership.Rating` starts at 1000 per circle; it is the per-circle ELO score (not a global user field).
- `Circle.IsPrivate` and `JoinCode` exist in the model but are not exposed in DTOs yet — reserved for a future invite-only join flow.
- ELO formula parameters: `amplifier=0.7`, `K=32` (48 for first 15 matches), initial rating 1000. See PRD §Algoritmo for the full formula.

### Config

- Dev connection string in `appsettings.json` points to `(localdb)\mssqllocaldb`. Override in `appsettings.Development.json` or user secrets.
- JWT `Secret` must be ≥ 32 chars. In dev, `appsettings.Development.json` has a placeholder; never commit a real secret.
- CORS allowed origins in `appsettings.json`; default is `http://localhost:4200`.
