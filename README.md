# Claims Module — FNOL & Reserve Management

A claims-intake and reserve-management module for a P&C insurer: log a First Notice of Loss (FNOL), track a claim through its status lifecycle, manage reserve components with role-gated approval, attach documents, and view a full audit trail.

Built for the DICEUS Fullstack Engineer Technical Assessment. Backend: .NET 9 / C# 13, Clean Architecture, CQRS via MediatR, EF Core 9, FluentValidation, AutoMapper, Hangfire, SQL Server. Frontend: Angular 18 (standalone components), Angular Material.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the solution design, data model, and the tradeoff decisions made where the two source specs disagreed. See [`AI-WORKFLOW.md`](AI-WORKFLOW.md) for how this was built.

---

## Prerequisites

- **.NET 9 SDK**
- **SQL Server** — LocalDB works fine for local dev (`Server=(localdb)\mssqllocaldb`)
- **Node.js 20+** and **npm** (Angular 18 requires Node ≥ 18.19)
- **Angular CLI 18** (`npx @angular/cli@18 ...` works without a global install)
- **Docker** — for `docker compose` and for the integration tests (Testcontainers starts SQL Server and Azurite)
- **Azure CLI** — only to provision the Azure deployment (`infra/deploy.sh`)

## Solution layout

```
ClaimsModule.sln
├── src/
│   ├── ClaimsModule.Domain           # entities, value objects, domain events — no dependencies
│   ├── ClaimsModule.Application      # CQRS commands/queries, validators, pipeline behaviors
│   ├── ClaimsModule.Persistence      # EF Core, migrations, seed data
│   ├── ClaimsModule.Infrastructure   # JWT auth, Hangfire jobs, blob/local file storage
│   └── ClaimsModule.API              # controllers, DI wiring, Program.cs
├── tests/
│   ├── ClaimsModule.Domain.Tests
│   ├── ClaimsModule.Application.Tests
│   └── ClaimsModule.Infrastructure.Tests
└── frontend/
    └── claims-module-ui/             # Angular app
```

## Quick start with Docker Compose

The fastest way to run the whole stack (SQL Server + API + frontend) with no local .NET/Node/SQL install at all:

```bash
docker compose up --build
```

This builds three images and starts four containers in dependency order: `sqlserver` (waits for a health check), a one-shot `migrations` container that applies EF Core migrations and seed data then exits, `api` (waits for migrations to succeed), and `frontend` (nginx serving the built Angular app). Once it settles:

- Frontend: `http://localhost:4200`
- API / Swagger: `http://localhost:5299/swagger`

Data persists in named volumes (`sqlserver-data`, `api-documents`) across restarts; `docker compose down -v` wipes both for a fully fresh database. Override the SA password, JWT signing key, or document-URL signing secret via a `.env` file or environment variables (`SQL_SA_PASSWORD`, `JWT_SIGNING_KEY`, `STORAGE_SIGNING_SECRET`) — the defaults in `docker-compose.yml` are dev-only placeholders, not meant for anything beyond local use.

This has been verified end-to-end against a fresh containerized SQL Server instance (not LocalDB): migrations + seed data apply cleanly, the API starts and connects Hangfire to the same SQL Server container, CORS and JWT auth work, and a full claim-creation round trip succeeds.

## Backend setup (without Docker)

1. **Restore & build**

   ```bash
   dotnet build ClaimsModule.sln
   ```

2. **Configure the database connection.** `src/ClaimsModule.API/appsettings.Development.json` already points at LocalDB:

   ```json
   "ConnectionStrings": {
     "ClaimsDatabase": "Server=(localdb)\\mssqllocaldb;Database=ClaimsModule;Trusted_Connection=True;MultipleActiveResultSets=true"
   }
   ```

   Point this at a different SQL Server instance if you're not using LocalDB. The same connection string is used for both EF Core and Hangfire's job storage.

3. **Apply migrations** (creates the database and seeds reference data — cause-of-loss codes, three sample policies, and the full status-transition graph for all three roles):

   ```bash
   cd src/ClaimsModule.Persistence
   dotnet ef database update --startup-project ../ClaimsModule.API
   ```

4. **Run the API:**

   ```bash
   cd src/ClaimsModule.API
   dotnet run
   ```

   - Swagger UI: `http://localhost:5299/swagger`
   - Hangfire dashboard (dev only): `http://localhost:5299/hangfire`
   - The API listens on `http://localhost:5299` by default (see `Properties/launchSettings.json`) and has CORS enabled for `http://localhost:4200` (the Angular dev server).

### Configuration reference (`appsettings.json` / `appsettings.Development.json`)

| Section | Key | Purpose |
|---|---|---|
| `ConnectionStrings` | `ClaimsDatabase` | SQL Server connection string (EF Core + Hangfire) |
| `Jwt` | `Issuer`, `Audience`, `SigningKey`, `ExpiryMinutes` | Mock JWT issuance (see below) — `SigningKey` must be at least 32 bytes for HMAC-SHA256 |
| `Storage` | `Provider` | `"LocalFileSystem"` (default, dev) or `"AzureBlob"` |
| `Storage` | `LocalFileSystemRootPath`, `LocalFileSystemPublicBaseUrl`, `LocalFileSystemSigningSecret` | Local storage fallback — signs time-limited download URLs to approximate the Azure SAS URL contract |
| `Storage` | `AzureBlobConnectionString`, `AzureBlobContainerName` | Used only when `Provider` is `"AzureBlob"` |
| `Cors` | `AllowedOrigins` (array) | Origins allowed to call the API. Defaults to `http://localhost:4200`; set `Cors__AllowedOrigins__0` in Azure |
| `Workflow` | `AllowTrivialClaimClosure` | `true` (default) allows the Open → Closed shortcut for trivial claims (BR-C-06) |
| `Hangfire` | `ServerEnabled` | `true` (default). The integration tests turn it off so enqueued jobs can be asserted on |

### Authentication (mock)

There's no real credential store — this is explicitly out of scope for the assessment. `POST /api/auth/mock-login` with `{ "role": "handler" | "supervisor" | "manager" }` returns a signed JWT for one of the hardcoded users. These are also the **test credentials** for the deployed app (§4.3 of the brief): there are no passwords, so pick the role on the login screen.

| Role key | User | Can approve/reject reserves? |
|---|---|---|
| `handler` | Hannah Handler | No |
| `supervisor` | Sam Supervisor | Yes |
| `manager` | Morgan Manager | Yes |
| `orgb-handler` | Blake OrgB (second organization) | No — exists to demonstrate tenant isolation |

`GET /api/auth/users` lists the valid role keys. The frontend's login screen is just a role switcher built on these two endpoints.

## Frontend setup (without Docker)

```bash
cd frontend/claims-module-ui
npm install
npx ng serve
```

Serves on `http://localhost:4200` and expects the API at `http://localhost:5299`. The API origin is read at runtime from `public/config.js`, which deployments overwrite, so one build can target any environment.

## Running tests

```bash
dotnet test ClaimsModule.sln              # unit, architecture and integration tests — Docker must be running (Testcontainers)
cd frontend/claims-module-ui
npm run lint && npm run test:ci           # frontend lint + unit tests
npm run e2e                               # Playwright, against a running API (:5299) + ng serve (:4200)
```

See `ARCHITECTURE.md` §11 for what each layer covers, why integration tests use Testcontainers rather than EF InMemory, and the smoke script (`scripts/smoke-test.sh`).

## Azure deployment

The brief (§3.8) asks for a public deployment. The infrastructure is code (`infra/main.bicep`), and one script provisions it:

| Resource | Used for |
|---|---|
| App Service (Linux, .NET 9, B1) with a system-assigned managed identity | The API, Swagger, and the Hangfire server (Always On keeps the recurring SLA job running) |
| Azure Static Web Apps (Free) | The Angular SPA. `staticwebapp.config.json` provides the SPA fallback; `config.js` is written at deploy time with the API URL |
| Azure SQL Database (Basic) | Application data and Hangfire job storage |
| Storage account, private `claim-documents` container | Claim documents; downloads use 1-hour SAS URLs |
| Key Vault (RBAC) | SQL connection string, storage connection string, JWT signing key. The App Service reads them through Key Vault references, so no secret sits in app settings |

To reproduce the deployment:

```bash
az login
NAME_PREFIX=claimsdemo RESOURCE_GROUP=claims-module-rg LOCATION=westeurope ./infra/deploy.sh
```

The script creates the resource group and deploys the template. It also creates a service principal scoped to that resource group, and prints the GitHub secrets and variables the pipeline needs; `SET_GITHUB_SECRETS=1` writes them with `gh` instead.

After that, run the **CI** workflow from the Actions tab (it also runs on every push to `main`). Its stages are:

1. Build.
2. Backend and frontend tests.
3. Migrate: an EF migration bundle is applied to Azure SQL through a firewall rule that exists only for that run.
4. Deploy: the API goes to App Service and the SPA to Static Web Apps.
5. Smoke tests (`scripts/smoke-test.sh`) against the public URLs.

**Deployed URLs:** _fill in after the first deployment_. They are the `apiUrl` / `frontendUrl` outputs printed by `infra/deploy.sh`, and Swagger is at `<apiUrl>/swagger`.

The SQL schema and seed data can also be applied without EF: see `database/README.md`.

## Feature walkthrough

1. **Sign in** as any role at `/login`.
2. **Claims list** (`/claims`) — filterable, paginated dashboard. Claims untouched for 48h in Draft/Open carry an **SLA breached** badge, set by the recurring Hangfire job. "Log New Claim" opens the FNOL intake form.
3. **FNOL intake** (`/claims/new`) — 3-step form: policy search (pick a result — the seeded policies are `POL-2026-000101` Acme Logistics, `POL-2025-000202` Harborview, `POL-2024-000303` Meridian, `POL-2026-000404` Short-Term Events — or switch on "Unknown policy") + loss details; parties (≥1 Claimant required) and risk objects; optional initial reserve with a live authority-tier preview, then review and submit.
4. **Claim detail** (`/claims/:id`) — status transitions (role-gated by the seeded transition table, confirmation dialog), and five tabs: Overview, Parties, Reserves (open reserves; adjust to a new amount with a reason, with history showing previous → new; approve/reject as Supervisor or Manager; GL-posting status), Documents (upload with a document type, download via signed URL), Audit Log (full history, paginated).

Try the reserve-approval flow across roles to see the authority thresholds in action: amounts ≤ $10,000 auto-approve, $10,000–$100,000 need Supervisor approval, above that needs Manager approval — sign in as `handler` to submit a large reserve, then switch to `supervisor` or `manager` to approve it and watch the GL-posting audit entry appear a moment later (it's processed async via Hangfire).

Two rules to expect while trying it:
- **No self-approval.** Approve is hidden on a change you requested yourself, because someone else has to approve it. You can still reject your own request to withdraw it.
- **One pending change at a time.** Adjust is disabled on a reserve until its pending change is approved or rejected.
