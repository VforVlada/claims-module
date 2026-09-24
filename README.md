# Claims Module — FNOL & Reserve Management

A claims-intake and reserve-management module for a P&C insurer: log a First Notice of Loss (FNOL), track a claim through its status lifecycle, manage reserve components with role-gated approval, attach documents, and view a full audit trail.

Built for the DICEUS Fullstack Engineer Technical Assessment. Backend: .NET 9 / C# 13, Clean Architecture, CQRS via MediatR, EF Core 9, FluentValidation, AutoMapper, Hangfire, SQL Server. Frontend: Angular 18 (standalone components), Angular Material.

**Live demo:** frontend https://purple-plant-0bef7350f.3.azurestaticapps.net · backend https://api-assess-28744.azurewebsites.net (see [§5](#5-azure-deployment)).

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the solution design, data model, and the tradeoff decisions made where the two source specs disagreed. See [`AI-WORKFLOW.md`](AI-WORKFLOW.md) for how this was built.

**Contents:** [1. Prerequisites](#1-prerequisites) · [2. Local development setup](#2-local-development-setup) · [3. Configuration](#3-configuration-environment-variables--appsettings) · [4. Database migrations & seed data](#4-database-migrations--seed-data) · [5. Azure deployment](#5-azure-deployment) · [6. Application walkthrough](#6-application-walkthrough) · [Running tests](#running-tests)

---

## 1. Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| **.NET SDK** | 9.0.x (developed on 9.0.305; CI uses `9.0.x`) | Building and running the API |
| **EF Core CLI** (`dotnet-ef`) | 9.x, matching EF Core 9.0 | Applying migrations: `dotnet tool install --global dotnet-ef --version "9.*"` |
| **SQL Server** | 2019+ or **LocalDB** (ships with Visual Studio / SQL Server Express) | Application data and Hangfire job storage |
| **Node.js** + **npm** | Node 20 LTS (CI uses 20; Angular 18 needs ≥ 18.19) | Building and serving the frontend |
| **Angular CLI** | 18 | Optional: `npx ng …` uses the project-local CLI, so no global install is needed |
| **Google Chrome** | Current | Frontend unit tests (Karma, ChromeHeadless) and Playwright E2E |
| **Docker Desktop** | Current, **running** | `docker compose`, and the integration tests (Testcontainers starts SQL Server and Azurite) |
| **Azure CLI** (`az`) + **OpenSSL** | Current | Only to provision Azure (`infra/deploy.sh`) |
| **GitHub CLI** (`gh`) | Current | Optional: lets `deploy.sh` write the pipeline secrets itself |

**Environment setup:** there is none beyond the tools. The API runs as `Development` via `launchSettings.json`, which loads `appsettings.Development.json`. That file already has a LocalDB connection string and dev-only signing keys, so no environment variables or secrets are needed to run locally.

## Solution layout

```
ClaimsModule.sln
├── src/
│   ├── ClaimsModule.Domain           # entities, value objects, domain events — no dependencies
│   ├── ClaimsModule.Application      # CQRS commands/queries, validators, pipeline behaviors, AutoMapper profiles
│   ├── ClaimsModule.Persistence      # EF Core DbContext, configurations, migrations, seed data
│   ├── ClaimsModule.Infrastructure   # JWT auth, Hangfire jobs, blob/local file storage
│   └── ClaimsModule.API              # controllers, middleware, DI wiring, Program.cs (startup project)
├── tests/
│   ├── ClaimsModule.Domain.Tests
│   ├── ClaimsModule.Application.Tests
│   ├── ClaimsModule.Infrastructure.Tests
│   ├── ClaimsModule.ArchitectureTests    # layering rules (NetArchTest)
│   └── ClaimsModule.IntegrationTests     # real SQL Server + Azurite via Testcontainers
├── frontend/claims-module-ui/        # Angular app (+ e2e/ Playwright specs)
├── database/                         # SQL exports of the migrations and seed data
├── infra/                            # main.bicep + deploy.sh (Azure)
├── scripts/smoke-test.sh             # post-deploy smoke tests
├── .github/workflows/ci.yml          # build → unit/integration/frontend tests → E2E
├── .github/workflows/deploy.yml      # manual: publish → migrate → deploy API + SPA
└── docker-compose.yml
```

## 2. Local development setup

### Option A — Docker Compose (whole stack, nothing else installed)

```bash
docker compose up --build
```

This builds three images and starts four containers in dependency order:
1. `sqlserver` starts first, and the rest wait for its health check.
2. A one-shot `migrations` container runs `dotnet ef database update`, which applies the migrations and seed data, then exits.
3. `api` waits for the migrations to succeed.
4. `frontend` is nginx serving the built Angular app.

Once it settles:

- Frontend: `http://localhost:4200`
- API / Swagger: `http://localhost:5299/swagger`

Data persists in the named volumes `sqlserver-data` and `api-documents` across restarts. `docker compose down -v` wipes both for a fresh database. The SA password, JWT signing key and document-URL signing secret can be overridden with `SQL_SA_PASSWORD`, `JWT_SIGNING_KEY` and `STORAGE_SIGNING_SECRET`, in a `.env` file or the environment. The defaults in `docker-compose.yml` are dev-only placeholders.

### Option B — run the API and frontend directly

1. **Clone, restore and build**

   ```bash
   git clone <repo-url> && cd ClaimsModule
   dotnet build ClaimsModule.sln
   ```

2. **Check the database connection.** `src/ClaimsModule.API/appsettings.Development.json` points at LocalDB:

   ```json
   "ConnectionStrings": {
     "ClaimsDatabase": "Server=(localdb)\\mssqllocaldb;Database=ClaimsModule;Trusted_Connection=True;MultipleActiveResultSets=true"
   }
   ```

   To use another SQL Server, change it there or override it with the environment variable `ConnectionStrings__ClaimsDatabase` (see [§3](#3-configuration-environment-variables--appsettings)). The same connection string is used by EF Core and by Hangfire's job storage.

3. **Create the database and apply migrations + seed data** (see [§4](#4-database-migrations--seed-data)):

   ```bash
   dotnet ef database update --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API
   ```

4. **Run the API**

   ```bash
   dotnet run --project src/ClaimsModule.API
   ```

   | URL | What |
   |---|---|
   | `http://localhost:5299/swagger` | Swagger UI; use `POST /api/auth/mock-login`, then **Authorize** with the token |
   | `http://localhost:5299/hangfire` | Hangfire dashboard (Development only) |
   | `http://localhost:5299/health` | Health check (database + document storage) |

   CORS allows `http://localhost:4200` (the Angular dev server) by default.

5. **Run the frontend** (in a second terminal)

   ```bash
   cd frontend/claims-module-ui
   npm ci
   npx ng serve
   ```

   Open `http://localhost:4200` and sign in by picking a role (see [Authentication](#authentication-mock)). The frontend expects the API at `http://localhost:5299`.

## 3. Configuration (environment variables / appsettings)

Settings are standard ASP.NET Core configuration: `appsettings.json` → `appsettings.{Environment}.json` → environment variables, with the last one winning. As an environment variable, a key's `:` becomes `__`, e.g. `Jwt:SigningKey` → `Jwt__SigningKey`. The base `appsettings.json` deliberately leaves the secrets empty. `appsettings.Development.json` fills them with dev-only values, and in Azure they come from Key Vault.

### API (`src/ClaimsModule.API`)

| Key (environment variable form) | Required | Example value | Purpose |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | — | `Development` / `Production` | `Development` loads `appsettings.Development.json` and enables the Hangfire dashboard |
| `ConnectionStrings__ClaimsDatabase` | **Yes** | `Server=(localdb)\mssqllocaldb;Database=ClaimsModule;Trusted_Connection=True;MultipleActiveResultSets=true` | SQL Server for EF Core and Hangfire. The API won't start without it |
| `Jwt__SigningKey` | **Yes** | `dev-only-signing-key-change-me-0123456789abcdef` | HMAC-SHA256 key for the mock JWTs. Must be ≥ 32 bytes. **Secret** |
| `Jwt__Issuer` | — | `ClaimsModule` | Token issuer (default shown) |
| `Jwt__Audience` | — | `ClaimsModule.Client` | Token audience (default shown) |
| `Jwt__ExpiryMinutes` | — | `480` | Token lifetime (default 8 h) |
| `Storage__Provider` | — | `LocalFileSystem` or `AzureBlob` | Where documents are stored (default `LocalFileSystem`). In Development, `AzureBlob` falls back to local disk (with a warning) when Azure/Azurite is unreachable |
| `Storage__LocalFileSystemRootPath` | — | `App_Data/claim-documents` | Local provider: folder for uploaded files |
| `Storage__LocalFileSystemPublicBaseUrl` | — | `/api/claims/local-documents` | Local provider: base path of the signed download URLs |
| `Storage__LocalFileSystemSigningSecret` | Local provider | `local-dev-only-signing-secret-change-me` | Local provider: signs time-limited download URLs, mimicking Azure SAS. **Secret** |
| `Storage__AzureBlobConnectionString` | When `AzureBlob` | `DefaultEndpointsProtocol=https;AccountName=claimsdemost…;AccountKey=…;EndpointSuffix=core.windows.net` | Blob provider: storage account. Must use an account key, because downloads are SAS URLs. **Secret** |
| `Storage__AzureBlobContainerName` | — | `claim-documents` | Blob provider: container, created on first upload |
| `Cors__AllowedOrigins__0` (`__1`, …) | In deployment | `https://claimsdemo-web-abc123.azurestaticapps.net` | Origins allowed to call the API (default `http://localhost:4200`) |
| `Workflow__AllowTrivialClaimClosure` | — | `true` | Allows the Open → Closed shortcut for trivial claims (BR-C-06). Default `true` |
| `Hangfire__ServerEnabled` | — | `true` | Runs the Hangfire job server in this process. The integration tests set `false` |
| `Logging__LogLevel__Default` | — | `Information` | Serilog / logging level |

Example: running the API against a different SQL Server with Azurite blob storage, using no appsettings changes:

```bash
export ConnectionStrings__ClaimsDatabase="Server=localhost,1433;Database=ClaimsModule;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
export Storage__Provider=AzureBlob
export Storage__AzureBlobConnectionString="UseDevelopmentStorage=true"
dotnet run --project src/ClaimsModule.API
```

### Frontend (`frontend/claims-module-ui/public/config.js`)

The API origin is read at runtime rather than compiled in, so one build can target any environment. The CI pipeline overwrites this file on deploy.

```js
window.__CLAIMS_CONFIG__ = {
  apiOrigin: 'http://localhost:5299'
};
```

### Authentication (mock)

There's no real credential store; the brief puts that out of scope. `POST /api/auth/mock-login` with `{ "role": "handler" }` returns a signed JWT for one of the hardcoded users. `GET /api/auth/users` lists the valid role keys, and the login screen is a role switcher built on these two endpoints. These are also the **test credentials** for the deployed app (§4.3 of the brief). There are no passwords; you pick a role.

| Role key | User | Can approve/reject reserves? |
|---|---|---|
| `handler` | Hannah Handler | No |
| `supervisor` | Sam Supervisor | Yes, up to $100,000 |
| `manager` | Morgan Manager | Yes, any amount |
| `orgb-handler` | Blake OrgB (second organization) | No. It exists to demonstrate tenant isolation |

## 4. Database migrations & seed data

EF Core migrations in `src/ClaimsModule.Persistence/Migrations` are the source of truth for both the schema and the seed data. The API does **not** migrate on startup. Migrations are applied explicitly, as below, by the Compose `migrations` container, or by the CI pipeline.

**Apply all migrations**, which creates the database if it doesn't exist. `ClaimsModule.API` is the startup project; it references `Microsoft.EntityFrameworkCore.Design` and supplies the connection string.

```bash
dotnet ef database update --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API

# against another server, without editing appsettings:
dotnet ef database update --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API \
  --connection "Server=myserver;Database=ClaimsModule;User Id=…;Password=…;TrustServerCertificate=True"
```

**Seed data** is part of the migrations (EF `HasData`, plus a hand-written `InsertData` step for policy coverages, since `HasData` does not support their complex `Money` properties), so `database update` seeds it too. There is no separate seed command. The seeded data is:

- **Cause-of-loss codes** per organization, including one owned by Org B that Org A can't use.
- **Claim status transitions:** the full transition graph for the Handler, Supervisor and Manager roles.
- **Four simulated policies** with coverages: `POL-2026-000101` Acme Logistics (in force through 2026), `POL-2025-000202` Harborview, `POL-2024-000303` Meridian and `POL-2026-000404` Short-Term Events. The last three have past or very short periods, so they exercise the "loss date outside the policy period" warning.

Other common tasks:

```bash
# list migrations and whether each is applied
dotnet ef migrations list --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API

# after changing the model: add a migration
dotnet ef migrations add <Name> --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API

# start over locally: drop the database, then run "database update" again
dotnet ef database drop --force --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API
```

**Without the EF tools:** `database/` holds an idempotent SQL script of every migration and a reference-data-only seed script. Run them with `sqlcmd -I`; see [`database/README.md`](database/README.md). **On Azure**, the *Deploy to Azure* workflow runs the same `dotnet ef database update` against Azure SQL (see §5).

## 5. Azure deployment

The brief (§3.8) asks for a public deployment. All infrastructure is code in `infra/main.bicep`, provisioned by `infra/deploy.sh`.

### Deployed resources

| Resource | SKU | Used for |
|---|---|---|
| App Service plan + App Service (Linux, .NET 9) with a system-assigned managed identity | B1 (`appServiceSkuName`) | The API, Swagger and the Hangfire server. Always On keeps the 15-minute SLA job running |
| Azure Static Web Apps | Free | The Angular SPA. `staticwebapp.config.json` gives the SPA route fallback, and `config.js` is written at deploy time with the API URL |
| Azure SQL server + database `ClaimsModule` | Basic (`sqlSkuName`) | Application data and Hangfire job storage. The firewall allows Azure services only |
| Storage account with a private `claim-documents` container | Standard_LRS | Claim documents. Downloads are 1-hour read-only SAS URLs |
| Key Vault (RBAC) | Standard | Secrets `ClaimsDatabase`, `StorageConnectionString` and `JwtSigningKey`. The App Service identity has *Key Vault Secrets User* and reads them through Key Vault references, so no secret is stored in app settings |

The App Service's app settings (the §3 keys) are `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__ClaimsDatabase`, `Jwt__SigningKey` and `Storage__AzureBlobConnectionString` (all three as `@Microsoft.KeyVault(...)` references), `Storage__Provider=AzureBlob`, `Storage__AzureBlobContainerName=claim-documents`, and `Cors__AllowedOrigins__0=<Static Web App URL>`.

### Reproducing the deployment

1. **Provision the infrastructure.** This step is idempotent, so re-running it updates the same resources.

   ```bash
   az login
   NAME_PREFIX=claimsdemo RESOURCE_GROUP=claims-module-rg LOCATION=westeurope ./infra/deploy.sh
   ```

   The script:
   - Creates the resource group and deploys `main.bicep`. If you don't supply `SQL_ADMIN_PASSWORD` and `JWT_SIGNING_KEY`, it generates them and prints them once.
   - Creates a service principal with Contributor on that resource group only.
   - Prints the GitHub secrets and variables the pipeline needs. With `SET_GITHUB_SECRETS=1`, it writes them with `gh` instead.

   | GitHub | Name |
   |---|---|
   | Secrets | `AZURE_CREDENTIALS`, `AZURE_SQL_CONNECTION_STRING`, `AZURE_STATIC_WEB_APPS_API_TOKEN` |
   | Variables | `AZURE_RESOURCE_GROUP`, `AZURE_SQL_SERVER_NAME`, `AZURE_WEBAPP_NAME`, `API_URL`, `FRONTEND_URL` |

2. **Test.** The **CI** workflow (`.github/workflows/ci.yml`) runs on every push and pull request: build, then backend tests (unit, architecture, integration), frontend tests, then E2E against the stack running under `docker compose`.

3. **Deploy** manually: Actions → **Deploy to Azure** (`.github/workflows/deploy.yml`) → *Run workflow*.
   1. **backend:** `dotnet publish` of the API, kept as an artifact.
   2. **migrate:** `dotnet ef database update` against Azure SQL, through a firewall rule for the runner's IP that exists only for that run.
   3. **deploy-backend:** the published API goes to App Service.
   4. **frontend** (runs in parallel with the above): `npm run build`, then `config.js` is written with the API URL and the SPA is uploaded to Static Web Apps.

4. **Smoke-test** the deployment: `API_URL=<apiUrl> FRONTEND_URL=<frontendUrl> ./scripts/smoke-test.sh`.

5. **Tear down** when finished: `az group delete --name claims-module-rg`.

**Deployed URLs:**

| | URL |
|---|---|
| Frontend | https://purple-plant-0bef7350f.3.azurestaticapps.net |
| Backend (API) | https://api-assess-28744.azurewebsites.net |
| Swagger | https://api-assess-28744.azurewebsites.net/swagger |
| Health check | https://api-assess-28744.azurewebsites.net/health |

These are the `frontendUrl` / `apiUrl` outputs printed by `infra/deploy.sh`.

## 6. Application walkthrough

1. **Sign in** (`/login`) as any role; see [Authentication](#authentication-mock).
2. **Claims list** (`/claims`) is a filterable (status, cause of loss, handler, date range), paginated dashboard. Claims untouched for 48 h in Draft/Open carry an **SLA breached** badge, set by a recurring Hangfire job. **Log New Claim** opens FNOL intake.
3. **FNOL intake** (`/claims/new`) is a three-step form:
   1. Policy and loss details. Search for a seeded policy (e.g. type `POL-2026`), or switch on **Unknown policy**. A loss date outside the policy period raises a warning.
   2. Parties (at least one Claimant is required) and risk objects.
   3. An optional initial reserve with a live authority-tier preview, then review and submit. The claim gets a sequential number like `CLM-2026-0000042`.
4. **Claim detail** (`/claims/:id`) offers status transitions, role-gated by the seeded transition table and confirmed in a dialog. It has five tabs:
   - **Overview**
   - **Parties** (add more)
   - **Reserves**: open a reserve; adjust it to a new amount with a reason, where history shows previous → new; approve or reject as Supervisor or Manager; GL-posting status.
   - **Documents**: upload with a document type; download via a time-limited signed URL.
   - **Audit Log**: every change, newest first, paginated.
5. **Errors** from the API appear as a snackbar with a plain-language message. Server errors show only a reference ID (the request's trace ID), never internal details.

**Try the reserve-approval flow across roles.** Amounts ≤ $10,000 auto-approve, $10,000–$100,000 need Supervisor approval, and anything above needs a Manager. Sign in as `handler` and submit a large reserve, then switch to `supervisor` or `manager` and approve it. A `GL_POSTING_SIMULATED` audit entry appears a moment later, because the posting is processed asynchronously by Hangfire. A claim's total reserves are capped at $10,000,000; only a Manager can override the cap.

Rules to expect while trying it:
- **No self-approval.** Approve is hidden on a change you requested yourself, because someone else has to approve it. You can still reject your own request to withdraw it.
- **One pending change at a time.** Adjust is disabled on a reserve until its pending change is approved or rejected.
- **Tenant isolation.** Sign in as `orgb-handler`: Org A's claims are not listed, and opening one by URL returns *not found*.

## Running tests

```bash
dotnet test ClaimsModule.sln              # unit, architecture and integration tests (Docker must be running)
cd frontend/claims-module-ui
npm run lint && npm run test:ci           # frontend lint + unit tests (ChromeHeadless)
npm run e2e                               # Playwright, against a running API (:5299) + ng serve (:4200)
```

See `ARCHITECTURE.md` §11 for what each layer covers, why integration tests use Testcontainers rather than EF InMemory, and the smoke script (`scripts/smoke-test.sh`).
