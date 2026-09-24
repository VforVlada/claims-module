# Architecture

| Topic | Where |
|---|---|
| Solution structure | [§1](#1-solution-structure) |
| CQRS flow: API → Application → Persistence | [§1.1](#11-cqrs-flow-how-a-command-travels) |
| Data model: key entities and relationships | [§2](#2-domain-model), [§3](#3-data-model-conventions-ef-core--sql-server) |
| Domain events: what is raised, how it is handled | [§2 Domain events](#domain-events) |
| Hangfire jobs: triggers and idempotency | [§7](#7-hangfire-jobs) |
| Azure architecture | [§12](#12-azure-architecture) |
| Key design decisions and tradeoffs | [§14](#14-key-design-decisions-and-tradeoffs), plus [§0](#0-source-of-truth-reconciliation) and [§13](#13-deviations-from-the-briefs-schema-and-why) |

## 0. Source-of-truth reconciliation

Two source documents informed this build: the FRS (`Claims_Module_Candidate_Specification.docx`) and the assessment brief (`DICEUS_Fullstack_Technical_Assessment.docx`). Where they disagreed, **the assessment brief won**, since it's the graded document.

| Point | FRS said | Assessment brief said | Decision |
|---|---|---|---|
| Status transitions | Full graph incl. `Reopened`/`Withdrawn` + closure checklist CC-01–04 | Simplified: Draft → Open → UnderInvestigation → PendingPayment → Closed, or Draft → Open → Closed directly | Simplified path implemented, plus `Reopened` (from Closed; Supervisor/Manager only) and `Withdrawn` (before payment). Open → Closed is the switchable trivial-claim shortcut (`Workflow:AllowTrivialClaimClosure`, default on); Draft → Closed is not allowed. CC-01–04 not built. |
| Self-approval block (submitter ≠ approver) | Explicit rule (BR-R-03) | Not mentioned | Implemented as a four-eyes rule in `ApproveReserveCommandHandler` (403). Rejecting your own request is allowed, which withdraws it. See §5. |
| Entity/field naming | `OrganisationId`, `OldValue`/`NewValue` | `OrganizationEntityId`, `OldValues`/`NewValues` | Assessment brief naming used throughout. |
| `ClaimStatusTransitions` table | Not modeled as a DB table | Explicit DDL table: FromStatus, ToStatus, RequiredPermission | Modeled as a real seeded table (`ClaimStatusTransitionSeed`), validated by `IClaimStatusTransitionValidator` — no hardcoded switch statement. |
| Reserve component types | Indemnity / Expense / ALAE / SubrogationRecoverable | IndemnityReserve / ExpenseReserve / RecoveryReserve / LitigationReserve | Assessment brief's four names used. `RecoveryReserve` is the only type allowed to be negative (subrogation/salvage). |
| `ClaimType` | Not present | Present on Claims | Included as an enum: Auto, Property, Liability, Other. |
| Authority thresholds | ≤$10K auto / >$10K–$100K supervisor / >$100K manager / >$10M aggregate → manager override | Same | No conflict — implemented as specified. |

## 1. Solution structure

Clean Architecture, dependency direction `API → Application → Domain`; `Infrastructure` and `Persistence` depend inward on `Application`/`Domain`, never the reverse. `API` is the only composition root.

```
src/
├── ClaimsModule.Domain          entities, value objects, domain events, exceptions — zero dependencies
├── ClaimsModule.Application     CQRS commands/queries, validators, pipeline behaviors, DTOs
├── ClaimsModule.Persistence     EF Core DbContext, entity configurations, migrations, seed data
├── ClaimsModule.Infrastructure  JWT auth, Hangfire jobs, blob/local file storage
└── ClaimsModule.API             controllers, middleware, Program.cs DI wiring (composition root)
tests/                           Domain / Application / Infrastructure unit tests, ArchitectureTests, IntegrationTests (§11)
frontend/claims-module-ui/       Angular SPA (§9)
infra/  database/  scripts/      Bicep + deploy script (§12), SQL exports of the migrations, smoke tests
```

Project references (arrows point at the dependency). They are enforced by `ArchitectureTests`, rules A-01..A-05:

```
                ┌──────────────► ClaimsModule.Infrastructure ─┐
ClaimsModule.API┤                                             ├──► ClaimsModule.Application ──► ClaimsModule.Domain
                ├──────────────► ClaimsModule.Persistence ────┘
                └────────────────────────────────────────────────► ClaimsModule.Application
```

Application owns every abstraction the outer layers implement. Infrastructure implements `ICurrentUserService`, `IStorageService`, `IBackgroundJobScheduler` and `IDateTimeProvider`. Persistence implements `IApplicationDbContext`, `IUnitOfWork`, `IAuditLogService` and `IClaimNumberGenerator`. That way handlers never see EF's SQL Server provider, Hangfire or the Azure SDK, and each can be replaced in tests.

Each command/query handler is paired with a FluentValidation validator (auto-discovered via `AddValidatorsFromAssembly`) and runs through a fixed pipeline (outer → inner, registration order in `Application/DependencyInjection.cs`):

```
LoggingBehavior → ValidationBehavior → UnitOfWorkBehavior → DomainEventDispatchBehavior → Handler
```

- **`UnitOfWorkBehavior`** wraps the handler in a transaction. It does not call `SaveChanges` itself — handlers call `IApplicationDbContext.SaveChangesAsync` directly so they get server-generated values (ids, row versions) back before building their response DTO. The transaction just makes that call and any other round trip in the same command (e.g. `IClaimNumberGenerator`'s sequence read) atomic together.
- **`DomainEventDispatchBehavior`** sits *inside* `UnitOfWorkBehavior`, so events are dispatched after the handler saves but before the commit. Event handlers' writes (audit rows) are atomic with the change that raised them: if an audit write fails, the whole command rolls back, so there is never a claim without its `CLAIM_CREATED` entry. Side effects outside the database are deferred to commit with `IUnitOfWork.OnCommitted` (and dropped on rollback). The Hangfire enqueue uses this, so a job never runs against uncommitted or rolled-back rows.
- Queries (anything not implementing `ICommand`) skip both behaviors entirely.

### 1.1 CQRS flow: how a command travels

Reads and writes are separate MediatR requests. Commands implement `ICommand` and queries implement `IQuery`, and each has its own handler. They share one `DbContext` and database: the split is logical, not two stores. Example: `POST /api/claims/{id}/reserves` (open a reserve).

```
HTTP ─► ReservesController ─► ISender.Send(OpenReserveCommand)
          │ [Authorize(Roles)]     │
          │                        ▼  MediatR pipeline (outer → inner)
          │   LoggingBehavior ─► ValidationBehavior ─► UnitOfWorkBehavior ─► DomainEventDispatchBehavior ─► OpenReserveCommandHandler
          │                        │ 400 on failure       │ BEGIN TRAN            │ (after handler returns)          │
          │                        │                      │                       │ publish events ─► audit rows     │ load Claim aggregate
          │                        │                      │                       │   (same tran)   ─► job enqueue   │ domain method mutates it
          │                        │                      │ COMMIT ◄──────────────┘                   deferred       │ SaveChangesAsync
          │                        │                      │  └─► OnCommitted: Hangfire enqueue                       │
          ◄────────────── Result<ReserveComponentDto> (+ non-blocking warnings) ────────────────────────────────────┘
```

1. **API.** The controller holds no logic. It checks the role (`[Authorize]`), binds the request to a command record and calls `ISender.Send`. Controllers never reference EF, `IApplicationDbContext` or `IUnitOfWork` (test A-03). Exceptions become ProblemDetails in `ExceptionHandlingMiddleware` (§4).
2. **`LoggingBehavior`** logs the request name, user and duration.
3. **`ValidationBehavior`** runs every `IValidator<OpenReserveCommand>`: shape, sign and length rules, plus async database checks such as "cause-of-loss code exists". It throws `ValidationException`, which becomes a 400 with per-field errors, before any transaction opens.
4. **`UnitOfWorkBehavior`** opens a database transaction (`IUnitOfWork.BeginTransactionAsync`).
5. **Handler** (Application):
   - loads the `Claim` aggregate through `IApplicationDbContext`; the tenant and soft-delete query filters apply automatically;
   - asks `IReserveAuthorityEvaluator` for the approval tier;
   - calls the domain method `claim.OpenReserve(...)`, which enforces the invariants and raises `ReserveSubmittedEvent`;
   - checks the $10M cap;
   - calls `SaveChangesAsync`, where `ClaimsDbContext` stamps audit columns and checks the tenant and soft-delete rules (§3).

   The handler maps the result to a DTO with AutoMapper; the profiles live in Application.
6. **`DomainEventDispatchBehavior`** collects the events from tracked aggregates, clears them, and publishes each through MediatR. Handlers write audit rows via `IAuditLogService`, inside the same transaction. The job-enqueue request is registered with `IUnitOfWork.OnCommitted`.
7. **Commit.** `UnitOfWorkBehavior` commits, and then the deferred Hangfire enqueue runs. Any exception at any step rolls everything back, and no job is enqueued.

**Read side.** A query such as `GET /api/claims` → `ListClaimsQuery` goes through Logging and Validation only. Its handler uses `AsNoTracking()` and `ProjectTo<TDto>()`, so SQL selects only the DTO's columns. No transaction is opened and no events are raised.

## 2. Domain model

```
Policy 1 ──── 0..* Claim ◆──── 1 LossEvent ──── * ─ 1 CauseOfLossCode
  │ 1          (aggregate  ◆──── * ClaimParty
  │            root)       ◆──── * ClaimRiskObject
  ▼ *                      ◆──── * ClaimDocument
PolicyCoverage             ◆──── * ClaimReserveComponent ◆──── * ReserveHistory
                           ┆
                           ┆ ClaimId (no FK) ─ * ClaimAuditLog
ClaimStatusTransition   (stand-alone workflow table: FromStatus → ToStatus per role)
```

`◆` marks ownership: the child is created and changed only through its aggregate root, and its FK cascades. Every table except `ClaimStatusTransitions` also carries `OrganizationEntityId` (tenant), the audit columns and the soft-delete columns from `BaseEntity` (§3).

| Entity | Key fields | Relationships / notes |
|---|---|---|
| **Claim** (aggregate root) | `ClaimNumber` (`CLM-YYYY-NNNNNNN`, unique), `ClaimType`, `Status`, `AssignedHandler`, `IsSlaBreached`/`SlaBreachedAt`, `RequiresManagerOverride`, `RowVersion` | Optional FK to **Policy**: unknown-policy intake is allowed. Owns every entity below |
| **LossEvent** | `LossDate`, `Location`, `Description` | Exactly one per claim. FK to **CauseOfLossCode** |
| **ClaimParty** | `PartyType` (Individual/Organization), `PartyRole` (Claimant, Witness, …), `Name`, `ContactEmail`, `ContactPhone` | ≥ 1 Claimant needed before a claim leaves Draft |
| **ClaimRiskObject** | `AssetType`, `Identifier` (e.g. VIN), `Description` | The insured item that was damaged |
| **ClaimReserveComponent** | `ComponentType` (Indemnity/Expense/Recovery/Litigation), `CurrentAmount` (Money, derived), `Status` (Open/Closed), `ApprovalStatus` (of the latest change), `RowVersion` (shadow) | One running balance per reserve type. `Status` follows the claim: it closes when the claim is Closed or Withdrawn and reopens on Reopened. A closed line refuses changes and approvals, but a pending change can still be rejected |
| **ReserveHistory** | `ChangeSequence`, `PreviousAmount`/`NewAmount`/`Amount` (Money), `RequiredTier`, `ApprovalStatus`, `PostingStatus`, `RequestedBy`/`DecidedBy`, `IdempotencyKey` (computed) | Insert-only log of proposed changes. Unique on (`ReserveComponentId`, `ChangeSequence`) |
| **ClaimDocument** | `FileName`, `SanitizedFileName`, `ContentType`, `DocumentType`, `SizeBytes`, `BlobPath` | Metadata only; the bytes live in Blob Storage (§8) |
| **ClaimAuditLog** | `Action`, `OldValues`, `NewValues`, `PerformedBy`, `IdempotencyKey` (unique when set) | Append-only. Linked by `ClaimId` without an FK constraint, so the audit trail doesn't depend on the claim row's lifecycle |
| **Policy** / **PolicyCoverage** | `PolicyNumber`, `ClientName`, `EffectiveDate`/`ExpirationDate` / `CoverageType`, `Limit`, `Deductible` (Money) | Simulated, seeded, read-only policy-admin data |
| **CauseOfLossCode** | `Code`, `Description`, `PerilCategory`, `IsActive` | Seeded per organization |
| **ClaimStatusTransition** | `FromStatus`, `ToStatus`, `RequiredPermission` (role) | Seeded, shared across tenants; drives `IClaimStatusTransitionValidator` |

Key invariants encoded in the model, not left to callers:

- **A reserve change proposes a new amount, not a delta.** Opening or adjusting a reserve submits the reserve's *new total* with a reason. The `ReserveHistory` row records `PreviousAmount` (the approved balance at submission), `NewAmount`, `Amount` (the change between them, which is what the GL posts), `ChangeReason`, and `RequestedBy` (the brief's ChangedBy). Authority (BR-R-02..04) is evaluated on the new reserve amount. Only one change can be pending per component, so `PreviousAmount` can't go stale while it waits.
- **Sign rules (BR-R-01, FRS A.2):** outstanding reserves must be greater than zero; a `RecoveryReserve` is money expected back, so it is carried as a negative amount. Validators return a 400 for the wrong sign, and the domain enforces the same rule again.
- **`ClaimReserveComponent.CurrentAmount` is derived, never directly written.** It's recomputed by `RecalculateBalance()` after every `ReserveHistory` insert or decision, as the sum of the changes that are `AutoApproved` or `Approved`, which equals the latest approved `NewAmount`. A `PendingApproval` change contributes nothing until it's decided.
- **`ReserveHistory` is insert-only.** No method rewrites an amount after creation; `Approve`/`Reject` only mutate status fields on that specific row. Resubmission after rejection (BR-R-06) creates a *new* history row — the rejected one stays `Rejected` forever.
- **`ClaimAuditLog` is append-only by convention** — every write goes through `IAuditLogService`, which only ever `Add`s rows. No handler or job touches `ClaimAuditLogs` directly.
- **Status transitions are data-driven.** `Claim.TransitionTo()` only enforces the one in-memory invariant that survives regardless of caller (BR-C-03: ≥1 Claimant party before leaving Draft). The actual "is this transition allowed for this role" check is `IClaimStatusTransitionValidator` querying the seeded `ClaimStatusTransitions` table — no hardcoded switch statement.

### Domain events

| Event | Raised when | Handler does |
|---|---|---|
| `ClaimCreatedEvent` | `CreateClaimCommand` succeeds | Audit log `CLAIM_CREATED` |
| `ClaimStatusChangedEvent` | `Claim.TransitionTo()` succeeds | Audit log `STATUS_CHANGED` (old → new status) |
| `PartyAddedEvent` | Party added to a claim | Audit log `PARTY_ADDED` |
| `ReserveSubmittedEvent` | Reserve opened or adjusted | Audit log `RESERVE_CREATED` or `RESERVE_AUTO_APPROVED`. If auto-approved, it also requests the GL-posting job, which is enqueued once the command commits |
| `ReserveApprovedEvent` | Supervisor/Manager approves a pending reserve | Audit log `RESERVE_APPROVED`, and requests the GL-posting job (enqueued on commit) |
| `ReserveRejectedEvent` | Reserve rejected | Audit log `RESERVE_REJECTED` (reason recorded) |
| `DocumentUploadedEvent` | Document stored successfully | Audit log `DOCUMENT_UPLOADED` |
| `ClaimWarningRaisedEvent` | A non-blocking rule fires: BR-C-02 (loss date outside the policy period) on creation, BR-R-07 (manager override of the $10M cap) on approval | Audit log `CLAIM_WARNING` |

**How they are raised and handled:**
1. Aggregates (`Claim`, `ClaimReserveComponent`) call `AddDomainEvent(...)` inside their own domain methods. So an event exists only if the state change it describes really happened, whichever handler caused it.
2. Events are plain records (`DomainEvent`), so Domain has no MediatR dependency (architecture rule A-01).
3. After the command handler returns, `DomainEventDispatchBehavior` reads the events from every tracked aggregate (`IApplicationDbContext.GetEntitiesWithDomainEvents`) and clears them so none is published twice. It wraps each in `DomainEventNotification<T>` and publishes it with `IPublisher`. It repeats until no new events are raised.
4. Handlers derive from `DomainEventHandler<T>` in Application (`Claims/EventHandlers`, `Reserves/EventHandlers`). They run **inside the command's transaction**, so the audit row is atomic with the change (tested: `DomainEventHandlerFails_CommandIsRolledBack`). Anything outside the database is deferred to commit via `IUnitOfWork.OnCommitted`.

The SLA breach is written by `SlaMonitoringJob` directly, not through an event, because the job runs outside the MediatR pipeline.

## 3. Data model conventions (EF Core / SQL Server)

- Primary keys: `uniqueidentifier`, client-generated (`Guid.NewGuid()` in `BaseEntity`'s initializer) — **not** database-generated (see the `ValueGeneratedNever()` note under §6).
- Monetary fields: modeled as the `Money` value object (`Amount` + `Currency`), mapped via EF Core's `ComplexProperty` — `decimal(19,4)`, never `money` or `float`.
- Timestamps: `datetimeoffset(7)`, stored UTC.
- Soft delete: `IsDeleted` + `DeletedAt`, enforced via a global query filter applied to every `BaseEntity`-derived type in `ClaimsDbContext.OnModelCreating`. `SaveChangesAsync` turns any `Remove()` (including cascade deletes) into a soft delete, so rows are never physically deleted. The audit log is still refused outright.
- Audit columns (`CreatedAt`, `UpdatedAt`, `UserCreated`, `UserModified`) are stamped automatically in `ClaimsDbContext.SaveChangesAsync` for `Added`/`Modified` entities — handlers never set them.
- Tenant isolation: `OrganizationEntityId` on every business table, enforced by a global query filter (combined with the soft-delete filter) on every `BaseEntity` type except the shared `ClaimStatusTransitions` table. `SaveChangesAsync` refuses to insert a tenant-owned row with an empty organization, since that row would be invisible to everyone. Two orgs are seeded (`1111…` default, `2222…` "Org B") so isolation can be tested and demoed. Background jobs have no current user, so they read with `IgnoreQueryFilters()` and re-apply the soft-delete condition themselves.
- Audit log is append-only: `IAuditLogService` only appends, and `SaveChangesAsync` rejects any modified or deleted `ClaimAuditLog` entry.
- Optimistic concurrency: a `RowVersion` **shadow property** (SQL Server `rowversion`), declared as `Property<byte[]>("RowVersion").IsRowVersion()` on `Claim` and `ClaimReserveComponent`. Only EF reads it, so it exists in the EF model but not on the domain classes. A stale update raises `DbUpdateConcurrencyException`, which becomes a 409 (I-DB-08).
- Fluent API only (`IEntityTypeConfiguration<T>` per entity) — no data-annotation attributes for schema.
- Claim numbers (`CLM-YYYY-NNNNNNN`, 7-digit sequence) are allocated via a SQL Server `SEQUENCE` (`ClaimNumberSequence`) read in a single round trip — never an application-level read-then-increment (BR-C-04).

## 4. API surface

| Method + route | Command/Query | Notes |
|---|---|---|
| `POST /api/claims` | `CreateClaimCommand` | Atomic claim number generation; optional initial reserve |
| `GET /api/claims` | `ListClaimsQuery` | Filter by status/date range/handler/cause code; paginated |
| `GET /api/claims/{id}` | `GetClaimDetailQuery` | Full aggregate: parties, risk objects, reserves + history |
| `PUT /api/claims/{id}/status` | `TransitionClaimStatusCommand` | 409 with allowed next statuses when the pair is not in the workflow; 403 when it is, but not for the caller's role |
| `GET /api/claims/{id}/audit` | `GetClaimAuditLogQuery` | Reverse-chronological, paginated |
| `POST /api/claims/{id}/parties` | `AddClaimPartyCommand` | |
| `POST /api/claims/{id}/documents` | `UploadClaimDocumentCommand` | Multipart; 50 MB max |
| `GET /api/claims/{id}/documents` | `GetClaimDocumentsQuery` | Returns a signed download URL per document, 1h TTL |
| `POST /api/claims/{id}/reserves` | `OpenReserveCommand` | Authority-threshold routing |
| `PUT /api/claims/{id}/reserves/{reserveId}` | `AdjustReserveCommand` | New `ReserveHistory` row; re-evaluates authority |
| `GET /api/claims/{id}/reserves` | `ListReservesQuery` | Current balances + full history |
| `POST /api/claims/{id}/reserves/{reserveId}/approve` | `ApproveReserveCommand` | Supervisor/Manager only |
| `POST /api/claims/{id}/reserves/{reserveId}/reject` | `RejectReserveCommand` | Supervisor/Manager only; reason required |
| `GET /api/policies/search` | `SearchPoliciesQuery` | Simulated, seeded dataset; matches number or client name (≥ 2 characters, wildcards literal). Returns `policyId`, `policyNumber`, `clientName`, `effectiveDate`, `expirationDate`, `status` (Active / Expired / NotYetEffective, from the effective period on the app clock) |
| `GET /api/policies/{id}/coverage` | `GetPolicyCoverageQuery` | Coverage type, limit, deductible, currency. Shown in FNOL step 1 and the review once a policy is picked; 404 for an unknown or other-tenant policy |
| `GET /api/reference/cause-of-loss-codes` | `ListCauseOfLossCodesQuery` | |
| `GET /api/reference/claim-statuses` | `ListClaimStatusesQuery` | Each status includes its allowed-next-statuses list |
| `POST /api/auth/mock-login` | — | Issues a JWT for `handler`/`supervisor`/`manager` (default org) or `orgb-handler` (second org) |

Errors are RFC 7807 ProblemDetails (`application/problem+json`, from `ExceptionHandlingMiddleware`, plus `UseStatusCodePages` for empty 401/403/404s): `{ type, title, status, instance, traceId, errors? }`, with `allowedNextStatuses` added for invalid status transitions. Status convention: **400** request validation (per-field `errors`), **401** unauthenticated, **403** role or authority (including a transition the role lacks), **404** missing or other-tenant resource, **409** state conflict (a transition not in the workflow, a stale rowversion), **422** any other business-rule violation (e.g. a reserve on a closed claim), **500** unexpected, never with a stack trace.

## 5. Business rules → enforcement

| Rule | Enforced by |
|---|---|
| BR-C-01 Loss date not in future | `CreateClaimCommandValidator` |
| BR-C-02 Loss date within policy period | `CreateClaimCommandHandler` — non-blocking warning, not a validator (cross-entity, doesn't block creation) |
| BR-C-03 ≥1 Claimant before leaving Draft | `Claim.TransitionTo()` — domain invariant, not a validator, since it must hold regardless of caller |
| BR-C-04 Atomic claim number | `IClaimNumberGenerator` — single `NEXT VALUE FOR` round trip |
| BR-C-05 Cause-of-loss code exists & active | `CreateClaimCommandValidator` (async DB check) |
| BR-C-06 Status path constraints | `IClaimStatusTransitionValidator` against the seeded table (the trivial Open → Closed shortcut is switched by `Workflow:AllowTrivialClaimClosure`), plus a domain guard in `Claim.TransitionTo()` that refuses Draft → Closed whatever the table says |
| BR-R-01 Reserve amount > 0 (RecoveryReserve may be negative) | `OpenReserveCommandValidator` / `AdjustReserveCommandValidator` / `CreateClaimCommandValidator` |
| BR-R-02/03/04 Authority thresholds | `ReserveAuthorityEvaluator` |
| BR-R-05 GL idempotency | `PostGlReserveChangeJob`: the unique `ClaimAuditLog.IdempotencyKey` index is the guard; `PostingStatus` is only a fast path (§7) |
| BR-R-06 Resubmission after rejection | `ClaimReserveComponent.SubmitChange()` — always appends a new history row |
| BR-R-07 $10M aggregate cap | `ReserveAuthorityEvaluator.ExceedsAggregateCap`, checked in `OpenReserveCommandHandler`/`AdjustReserveCommandHandler`/`CreateClaimCommandHandler`, and again in `ApproveReserveCommandHandler` — see the note below |
| Four-eyes: no self-approval | `ApproveReserveCommandHandler` — 403 when the approver requested the change (rejecting your own request is allowed, i.e. withdrawing it) |
| One outstanding change per component | `ClaimReserveComponent.SubmitChange()` — 422 while a change is `PendingApproval` |
| Reserve amount ≤ 1,000,000,000 (absolute) | `ReserveLimits.MaxAmount`, in the Open/Adjust/CreateClaim validators — keeps oversized values from overflowing the `DECIMAL(19,4)` columns as a 500 |
| Currency is a 3-letter code | `CurrencyRules.Pattern` (`^[A-Za-z]{3}$`), in the Open/Adjust/CreateClaim validators |
| Party phone: digits and an optional leading `+` | `ContactRules.PhonePattern`, in the AddClaimParty/CreateClaim validators |
| Text length ≤ column size | `MaximumLength` rules matching each `HasMaxLength` (handler 255, party name/email 255, phone 50, loss location 500, description 2000, risk object description 1000 / identifier 255, reasons 1000) |

> **BR-R-07 and pending amounts:** the cap is checked against `CurrentAmount`, which excludes `PendingApproval` changes, so a large pending change can't trip it when submitted. It is therefore re-checked on approval, the moment the amount joins the balance. Crossing the cap then needs `ManagerOverrideConfirmed: true` from a **Manager** (otherwise 422, or 403 for a non-Manager). The approval flags the claim `RequiresManagerOverride` and writes a `CLAIM_WARNING` audit entry. Open and Adjust apply the same Manager-only rule to the override flag, so no role can bypass the cap by sending the flag straight to the API.

## 6. Notable implementation decisions & pitfalls

- **`ClaimNumberGenerator` and `SqlQueryRaw`.** The original implementation composed `.FirstAsync()` onto `Database.SqlQueryRaw<long>("SELECT NEXT VALUE FOR ...")`. EF Core translates that composition by wrapping the raw SQL in a `TOP(1)` derived table — and SQL Server explicitly forbids `NEXT VALUE FOR` inside a derived table (error 11719). This silently broke *all* claim creation until caught by an end-to-end HTTP test against a real LocalDB instance (`dotnet build`/unit tests alone never touched this path). Fixed by materializing with `ToListAsync()` and indexing `[0]` instead, which executes the raw SQL as-is with no wrapping.
- **`BaseEntity.Id` needs `ValueGeneratedNever()`.** All primary keys are client-generated GUIDs (set in `BaseEntity`'s property initializer), but without an explicit `ValueGeneratedNever()` on every `BaseEntity`-derived type, EF Core's default GUID-key convention assumes the ID *might* be database-generated. That assumption breaks a specific, easy-to-hit scenario: loading an *existing* tracked aggregate (e.g. a `Claim`) and adding a *new* child to its collection (e.g. opening a reserve) — EF sees a non-default key on the new entity discovered via navigation fixup and assumes it already exists, emitting an `UPDATE` instead of an `INSERT`, which then throws `DbUpdateConcurrencyException` (0 rows affected) since no such row exists yet. This only manifests on the *second* write to an aggregate (new claim + its own children insert together as one Added subtree and work fine); it's fixed centrally in `ClaimsDbContext.OnModelCreating` by looping over every `BaseEntity` type and marking `Id` as never-generated.
- **EF Core's InMemory provider can't materialize `ComplexProperty` entities at all** — not just via `Include`, but on a plain `FirstOrDefaultAsync` against a `DbSet` whose entity has one (`ClaimReserveComponent.CurrentAmount`, `ReserveHistory.Amount`), throwing `KeyNotFoundException` from its query shaper. This is a known, still-open EF Core limitation (dotnet/efcore#31464), not a bug in this codebase. `Application.Tests` and `Infrastructure.Tests` use SQLite-in-memory instead, via a hand-rolled `TestDbContext : DbContext, IApplicationDbContext` (the real, sealed `ClaimsDbContext` can't be reused for this: it declares a SQL Server `SEQUENCE` that SQLite's migrations generator rejects outright). One query (`SlaMonitoringJob`'s `(c.UpdatedAt ?? c.CreatedAt) < staleBefore` inside a compound boolean `Where`) SQLite's provider in turn can't translate — that one test class falls back to the EF InMemory provider instead, safely, since it never touches a `ComplexProperty` entity.
- **Angular's `mat-tab-group` needs a bound `selectedIndex`.** Without `[(selectedIndex)]`, reloading the claim after any action (approving a reserve, adding a party, uploading a document) silently reset the visible tab back to Overview — caught by live browser testing, not by `ng build`.

## 7. Hangfire jobs

Hangfire runs inside the API process, with its job storage in the same SQL database (a `HangFire` schema). Application code never touches Hangfire: it calls `IBackgroundJobScheduler`, and job classes are resolved from DI in their own scope, with no HTTP user.

| Job | Type | Triggered by | Idempotency |
|---|---|---|---|
| `PostGlReserveChangeJob` | Fire-and-forget, 3 automatic retries | `ReserveSubmittedEvent` (auto-approved) or `ReserveApprovedEvent`. Enqueued through `IBackgroundJobScheduler` → `IUnitOfWork.OnCommitted`, i.e. only after the approving transaction commits | A unique key per reserve change, enforced by a unique index |
| `SlaMonitoringJob` | Recurring, every 15 minutes | Registered at startup (`RecurringJobRegistration`, `AddOrUpdate`, so restarts or extra instances never duplicate the schedule) | The `IsSlaBreached` flag |

### `PostGlReserveChangeJob`

Simulates posting a reserve change to the general ledger. Triggered by `ReserveSubmittedEvent` (when auto-approved) or `ReserveApprovedEvent`, via `IBackgroundJobScheduler.EnqueuePostGlReserveChange`.

- **Idempotency key:** `ReserveHistory.IdempotencyKey` → `"Reserve:{ComponentId}:Change:{ChangeSequence}"`.
- **Guard:** a unique filtered index on `ClaimAuditLog.IdempotencyKey`. The `PostingStatus == Posted` check up front is only a fast path: two concurrent executions can both pass it. The status update and the audit insert commit in one save, so a racing duplicate collides on the index, rolls back, and exits cleanly. A failed save counts as a duplicate only if a re-read finds the key already posted; any other `DbUpdateException` (timeout, deadlock) fails the attempt, so Hangfire retries it. This is covered by I-JOB-02 (sequential) and I-JOB-03 (`Task.WhenAll`, real SQL Server) in `GlPostingJobTests`.
- **Tenant:** the job runs with no HTTP user, so it reads past the tenant query filter; its audit row inherits the claim's organization.
- **Success:** marks `PostingStatus = Posted` and writes a `GL_POSTING_SIMULATED` audit entry. The entry is the simulated journal from the brief: `DR Change in Outstanding Reserves / CR Outstanding Loss Reserves` for the change amount, with the two lines reversed for a reserve decrease. It carries the idempotency key.
- **Failure:** `[AutomaticRetry(Attempts = 3)]` retries. Earlier attempts leave the posting `Pending`. Only the final attempt (`RetryCount` = 3) sets `PostingStatus = Failed` and writes a `GL_POSTING_FAILED` audit entry, in one save.
- **Known gap:** the enqueue happens just after commit, not through an outbox. If the process dies between commit and enqueue, the posting stays `Pending` and nothing re-enqueues it.

### `SlaMonitoringJob` (recurring, every 15 minutes)

Registered at startup by `RecurringJobRegistration` (`*/15 * * * *`, I-JOB-10). It is cross-tenant by design: it scans every organization's claims and records each breach under that claim's own organization.

Flags claims stuck in `Draft`/`Open` for 48h+ (brief §3.5). It sets `Claim.IsSlaBreached`/`SlaBreachedAt` and writes one `SLA_BREACH_DETECTED` audit entry, both in the same save. The status itself is unchanged: SlaBreached is a flag on top of the workflow status, not an eighth status.

- **Dedup:** the flag is the dedup. Already-flagged claims are skipped, so the 15-minute schedule never re-audits a claim that is still breached.
- **Clearing:** any activity on the claim (a transition, a party, a document or a reserve) clears the flag, so a claim that later goes stale again is a new breach.
- **The staleness clock:** setting the flag is bookkeeping, not activity. `ClaimsDbContext` doesn't stamp `UpdatedAt` when those are the only properties that changed, because stamping it would reset the clock the job measures.

## 8. Storage

`IStorageService` has two implementations, chosen at startup by `StorageServiceFactory` from `Storage:Provider` (`AzureBlob` | `LocalFileSystem`). In **Development**, `AzureBlob` is probed once, and if there is no connection string or the account is unreachable (Azurite not running), the API falls back to local disk and logs a warning. Everywhere else `AzureBlob` has no fallback: a misconfigured deployment fails at startup or turns `/health` unhealthy rather than quietly writing documents to the App Service disk.

- **`AzureBlobStorageService`** (production) — blob path `claim-documents/{organizationId}/{claimId}/{sanitizedFilename}`, retrieval returns a SAS URL with a 1-hour TTL.
- **`LocalFileSystemStorageService`** (dev fallback) — writes to disk under `Storage:LocalFileSystemRootPath`, and approximates the SAS URL contract with an HMAC-signed, time-limited query string (`LocalDocumentUrlSigner`), validated by `DocumentsController`'s dev-only, anonymous download endpoint.

Either way, the API never proxies document bytes for the "real" download path — the client gets a URL and fetches directly. Filenames are sanitized before storage; uploads are capped at 50 MB.

## 9. Frontend

Angular 18, standalone components, Angular Material, signals for local view state, Reactive Forms for the FNOL intake and inline edit forms. No NgRx — state is either server-fetched per view or held in small component signals; nothing in this app's scope needed cross-feature shared state complex enough to justify it.

```
frontend/claims-module-ui/src/app/
├── core/         mock-role AuthService, functional HTTP interceptors (auth + error), route guard,
│                 typed API services (one per controller — no direct HttpClient calls in components)
├── shared/       models mirroring every backend DTO/enum exactly, status-badge component,
│                 reusable confirm-dialog, snackbar service for API error/warning surfacing
└── features/
    ├── login          role switcher backed by GET /api/auth/users
    ├── claims-list     filterable, paginated dashboard
    ├── fnol-intake     3-step stepper (policy/loss → parties/risk objects → reserve/review)
    └── claim-detail    header (status transition, confirm dialog) + 5 tabs
```

**Forms mirror the API's rules so problems show inline before submitting** (the server still re-validates everything):

- Save/Next/Submit stay disabled until every required field is valid. The FNOL initial reserve's amount and currency are only required once its toggle is on.
- Step 1 needs a policy *picked* from the typeahead, or "Unknown policy" switched on. Typed text alone links nothing, and an empty search says "No matching policies". Picking a policy loads its coverages (`GET /api/policies/{id}/coverage`), which step 1 and the review both show.
- Every free-text input has a `maxlength` from `shared/models/field-limits.ts` (the column sizes). Long fields show a character counter.
- The `appPhoneInput` and `appCurrencyInput` directives filter keystrokes and pastes (digits plus a leading `+`; three upper-cased letters). The contact-email validator requires a dotted domain.
- Reserves: Adjust is disabled while a change is pending, and Approve is hidden on your own request. Opening, adjusting or approving past the $10M cap offers a Manager the override dialog.
- Every dropdown is alphabetical (`optionsByLabel` / `sortByLabel` in `enums.ts`). Reserve history lists the newest change first.

The FNOL form's reserve step and the claim-detail Reserves tab both compute a client-side authority-tier preview (`estimateApprovalTier` in `shared/models/enums.ts`) mirroring `ReserveAuthorityEvaluator`'s thresholds — it's a UX preview only; the server re-evaluates authoritatively on submit.

## 10. What's not built

- Real authentication/credential store (explicitly out of scope — mock JWT only).
- The FRS's CC-01–04 closure checklist — see §0. (`Reopened`/`Withdrawn` and the self-approval block now exist.)
- **A live Azure environment.** Everything needed to provision one is written (`infra/main.bicep`, `infra/deploy.sh`, and the CI deploy stages; see §12), but it has not been run against a subscription yet.

## 11. Testing

The testing plan (`DICEUS Claims Module — Testing Plan`) is implemented layer by layer. Test names and summaries carry the plan's IDs (U-C-*, U-W-*, U-R-*, I-DB-*, I-API-*, I-JOB-*, I-ST-*, A-*, F-*, E2E-*, S-*), so you can grep for any requirement.

| Layer | Project / location | Tooling | Runs against |
|---|---|---|---|
| Backend unit | `tests/ClaimsModule.Domain.Tests`, `Application.Tests`, `Infrastructure.Tests` | xUnit, Moq | Pure domain objects; SQLite-in-memory `TestDbContext` for handlers |
| Backend integration | `tests/ClaimsModule.IntegrationTests` | `WebApplicationFactory`, Testcontainers (SQL Server 2022 + Azurite), Respawn | The real `ClaimsDbContext`, migrations, filters, middleware, Hangfire storage and both storage providers |
| Architecture | `tests/ClaimsModule.ArchitectureTests` | NetArchTest.Rules | Compiled assemblies (A-01..A-05 plus naming) |
| Frontend unit | `frontend/claims-module-ui/src/**/*.spec.ts` | Karma/Jasmine, Angular Material harnesses, `HttpTestingController` | Components, services, interceptors |
| E2E | `frontend/claims-module-ui/e2e` | Playwright | A running stack (`E2E_BASE_URL`, `E2E_API_URL`) |
| Smoke | `scripts/smoke-test.sh` | curl + the Playwright `@smoke` subset | A deployed environment |

**Why Testcontainers rather than EF InMemory.** This is a deliberate decision. The rules most worth testing live in SQL Server itself:
- `rowversion` concurrency (I-DB-08)
- the claim-number `SEQUENCE` under 20 parallel requests (I-API-04)
- the unique index that makes GL posting idempotent under a real race (I-JOB-03)
- `DECIMAL(19,4)` / `DATETIMEOFFSET(7)` / `NVARCHAR(n)` types, checked against `INFORMATION_SCHEMA` (I-DB-02)
- the global tenant and soft-delete filters

The InMemory provider ignores or cannot express every one of these. It also cannot materialize this model's `ComplexProperty` money values at all (§6). The fast unit suites keep SQLite for handler logic. Anything that depends on the database's behavior runs in the integration project against real SQL Server.

**Integration-harness notes:**
- **Settings.** The factory runs the API in a `Testing` environment and passes settings with `UseSetting`. `ConfigureAppConfiguration` is applied too late, because `Program.cs` reads the connection string while registering services, so the old harness silently migrated the developer's LocalDB.
- **Hangfire.** Hangfire's processing server is turned off (`Hangfire:ServerEnabled=false`), so enqueued jobs stay queued and can be asserted on. Job classes are run directly from a DI scope with no HTTP user, exactly as Hangfire runs them. One test starts a real `BackgroundJobServer` to prove the whole wiring works.
- **Collections run one at a time.** Hangfire keeps its storage in a process-wide static (`JobStorage.Current`), so two hosts with different databases cannot run side by side.
- **Seeding.** Tests that seed through a context use `Factory.CreateDbContext(orgId)`, because a DI-resolved context outside a request correctly sees no tenant's rows.
- **Seed data for tests.** Org B, an inactive cause code, an Org B-owned code, and a narrow-window policy (`POL-2026-000404`) are seeded by migration. `ClaimBuilder` and `ReserveBuilder` live in `IntegrationTests/Infrastructure/TestData.cs`.

**Coverage.** CI enforces ≥ 90% line coverage on Domain + Application, where the business rules live. At the time of writing, the unit and integration suites together reach 96.1%. There is deliberately no target for API or Infrastructure plumbing.

**Out of scope (deliberate):** load and performance testing, penetration testing, and cross-browser matrices (E2E runs on Chromium only). These are real concerns for production, but they are outside this assessment's timebox. They would each need their own environment and tooling (e.g. k6 or NBomber, OWASP ZAP, a Playwright browser matrix) rather than more tests in this repo.

**Running everything locally:**

```bash
dotnet test ClaimsModule.sln                          # unit + architecture + integration (needs Docker running)
cd frontend/claims-module-ui
npm run lint && npm run test:ci                       # frontend lint + unit
npm run e2e                                           # needs the API on :5299 and `ng serve` on :4200
API_URL=http://localhost:5299 FRONTEND_URL=http://localhost:4200 ../../scripts/smoke-test.sh
```

On a machine without Playwright's bundled Chromium, set `E2E_CHROME_CHANNEL=chrome` to use the installed Chrome.

## 12. Azure architecture

```
          Browser
          │  static files                      │  HTTPS + Bearer JWT (CORS: SWA origin only)
          ▼                                    ▼
 Azure Static Web Apps  ───config.js────▶  App Service (Linux, .NET 9, B1, Always On)
 (Angular SPA, Free)      (API origin)      API + Swagger + Hangfire server (SLA job, GL jobs)
                                             │ managed identity   │ SQL auth         │ account key
                                             ▼                    ▼                  ▼
                                        Key Vault (RBAC)     Azure SQL Database   Storage account
                                        secrets → app         app data + Hangfire  claim-documents
                                        settings via          schema               container (private;
                                        Key Vault refs                             1h SAS for download)
```

- **Provisioning:** `infra/main.bicep` (linted clean), run by `infra/deploy.sh`. The script is idempotent, creates the resource group, and scopes a CI service principal to that resource group only.
- **Secrets:** the SQL connection string, the storage connection string and the JWT signing key live in Key Vault. The App Service's system-assigned identity has *Key Vault Secrets User*, and its app settings are `@Microsoft.KeyVault(...)` references, so no secret value sits in app configuration or in the repo.
- **Storage:** the container is private. The API hands out 1-hour SAS URLs signed with the account key (brief §3.6); the local-filesystem provider imitates the same contract with HMAC-signed URLs.
- **Hangfire:** runs inside the API process on the SQL database. Always On (B1) keeps the process alive for the 15-minute SLA job. A larger deployment would move the server into its own App Service or Container App.
- **Pipeline:** two workflows. `.github/workflows/ci.yml` runs build → tests (with a coverage gate) → E2E on every push and pull request. `.github/workflows/deploy.yml` ("Deploy to Azure") is run manually (*Run workflow*):
  - **backend** publishes the API once, as an artifact.
  - **migrate** runs `dotnet ef database update` against Azure SQL. The runner's IP is added to the SQL firewall for that step and removed again in an `always()` step.
  - **deploy-backend** deploys that artifact to App Service, after the migration.
  - **frontend** runs in parallel: the SPA is built once, gets its `config.js` for this environment, and is uploaded to Static Web Apps.

  Smoke tests (`scripts/smoke-test.sh`) are run by hand against the public URLs afterwards.
- **Test credentials:** mock login by role. `handler` is the standard handler; `supervisor` and `manager` can approve reserves.

## 13. Deviations from the brief's schema, and why

The brief says the schema should be derived from the DDL, not copied. These are the deliberate differences a reviewer might notice:

| Brief / DDL | Here | Why |
|---|---|---|
| ClaimParties `FirstName`, `LastName`, `ContactInfo` | `Name`, `ContactEmail`, `ContactPhone` | Parties include organizations (insurers, repairers) as well as people, and a single name fits both. Typed contact fields validate better than one free-text field. |
| ClaimRiskObjects `InsuredAssetType`, `AssetReference`, `DamageDescription` | `AssetType`, `Identifier`, `Description` | Same concepts, shorter names; the enum is `AssetType`. |
| ClaimAuditLog `EventType`, `TriggeredBy` | `Action`, `PerformedBy`, plus a unique `IdempotencyKey` | The idempotency key is how GL posting stays single under concurrency (§7). |
| Claims `LossDate`, `CauseOfLossCode` | On `LossEvents` (1:1 with the claim), not duplicated on `Claims` | The brief lists them on both tables. Keeping one copy means the loss date and cause can never disagree; the claims list reads them through the join. |
| ClaimDocuments `BlobReference`, `UploadedAt` | `BlobPath`, `CreatedAt` (+ `UploadedBy`) | The path *is* the blob reference within the container. The upload time is the row's audit timestamp, so a second column would only duplicate it. |
| ReserveHistory `ChangedBy` | `RequestedBy`, plus `DecidedBy`/`DecidedAt` | A change has two actors under the approval workflow, the requester and the approver, so one field could not record both. |
| ClaimReserveComponents `Status`, `ApprovalStatus` | Both present. `ApprovalStatus` is kept in step with the latest `ReserveHistory` row; `Status` follows the claim lifecycle | Stored rather than computed so the approval queue and reporting can filter on columns. The domain keeps them consistent, and the migration backfilled existing rows. |
| Persistence "repositories" | Handlers use `IApplicationDbContext` (EF's DbSets) behind an Application-layer interface; `IUnitOfWork` owns the transaction | `DbContext` already is a repository plus unit of work. Wrapping each DbSet again would add a layer without adding isolation. Handlers are unit-tested against SQLite through the same interface, and `UnitOfWorkBehavior` coordinates the transaction across every write in a command (tested: I-DB-09, JOB-04). |
| PKs `NEWSEQUENTIALID()` | Client-generated `Guid.NewGuid()` (`ValueGeneratedNever`) | Aggregates need their ids before saving, e.g. for idempotency keys and domain events. Random GUIDs fragment clustered indexes at scale; the upgrade is a sequential client-side generator, not a database default. |
| Free text `NVARCHAR(MAX)` | Bounded, e.g. descriptions 2000, reasons 1000 | Bounded free text is validated at the API edge; nothing in scope needs unbounded text. |
| Claim number unique per organization | Unique globally | A single global `SEQUENCE` issues every number, so global uniqueness holds and implies per-organization uniqueness. |
| SlaBreached "status" | A flag (`IsSlaBreached`) alongside the workflow status | A breach is an overlay on any Draft/Open claim. Making it a status would need a transition back out of it, and would hide the real workflow position. |
| Lazy-loaded feature *modules* | Lazy-loaded standalone components (`loadComponent`) | Angular 18's recommended equivalent. Each feature is still its own lazily fetched chunk. |

## 14. Key design decisions and tradeoffs

The decisions a reviewer is most likely to question, with what each one gave up. Details are in the linked sections.

| Decision | Why | Tradeoff / what it costs |
|---|---|---|
| **Domain events dispatched inside the command's transaction; Hangfire enqueued after commit** (`IUnitOfWork.OnCommitted`) | An audit entry can never be lost when its change commits, and a job never runs against uncommitted or rolled-back rows (§1.1). | No outbox: a crash between commit and enqueue leaves a GL posting `Pending` with nothing to re-enqueue it (§7). Fixing that needs an outbox table or a sweeper job. Also, a failing audit write now fails the user's request rather than being lost silently, which is the intended choice. |
| **`DbContext` as unit of work; no per-entity repositories** | EF's `DbSet` + change tracker already is repository + unit of work. `IApplicationDbContext` keeps handlers off the concrete provider, and they are tested against SQLite through it (§13). | Handlers can write LINQ against any set, so aggregate boundaries are kept by convention and by domain methods, not by the type system. |
| **`UnitOfWorkBehavior` owns the transaction but not `SaveChanges`** | Handlers need server-generated values (row versions, sequence numbers) before building the response, and some commands make raw-SQL round trips (the claim-number `SEQUENCE`) that must share the transaction. | A handler that forgets `SaveChangesAsync` persists nothing, and nothing in the pipeline would catch it. |
| **Validation in the MediatR pipeline, domain invariants in the entities** | Validators give 400s with per-field messages before any transaction opens. Rules that must hold whoever the caller is (≥ 1 Claimant, one pending reserve change, sign rules) live in domain methods (§5). | Some rules are checked twice, by a validator for the message and by the domain for safety. That duplication is deliberate. |
| **Data-driven status workflow** (`ClaimStatusTransitions` table) | The brief models it as a table. Changing who may make a transition is a data change, not a code change. | The graph is only as right as the seed data, and there is no admin UI to change it: a change means a new migration. |
| **A reserve change proposes a new total; `CurrentAmount` is derived** | This matches how adjusters think ("reserve is now $40k"). Authority is evaluated on the new total, and the history can always be replayed to the balance (§2). | The GL needs the delta, so each `ReserveHistory` row stores previous, new and the change. |
| **One pending change per reserve component** | `PreviousAmount` can't go stale while a change waits for approval, and approvals can't conflict. | A handler must wait for the decision before submitting another change to the same component. |
| **GL idempotency via a unique index, not a status check** | A status read-then-write races under Hangfire retries. A unique `IdempotencyKey` on the audit row makes the database the arbiter (§7). The key is deterministic (`Reserve:{ComponentId}:Change:{Seq}`), so a retry maps to the same key. | The audit table doubles as the posting ledger. A real GL integration would send the same key to the external system. |
| **Tenant isolation and soft delete as global query filters** | No handler can forget the tenant or deleted-row predicate. A `Remove()` becomes a soft delete centrally (§3). | Code with no user (background jobs) must use `IgnoreQueryFilters()` and re-apply soft delete by hand. The jobs do this, and the tests cover it. |
| **Shadow property where only EF needs the value; ordinary properties where code does** | The concurrency token (`RowVersion`) is read by nothing but EF, so it is a shadow property and stays off the domain model. Audit columns, the tenant id and FKs *are* used in LINQ filters, DTO projections and the SLA job, so they stay ordinary properties on `BaseEntity` and the children. | The domain classes still carry some persistence-flavoured fields (`CreatedAt`, `OrganizationEntityId`), because hiding them would force `EF.Property<T>(e, "...")` strings into every query that filters on them. |
| **Client-generated GUID keys** (`ValueGeneratedNever`) | Aggregates need their ids before saving, for idempotency keys and domain events (§6, §13). | Random GUIDs fragment clustered indexes at scale. The upgrade is a sequential client-side generator. |
| **Money as a value object** (`ComplexProperty`, `decimal(19,4)` + currency) | No floating-point money, and currency travels with the amount. | EF's InMemory provider can't materialize complex properties, which pushed tests to SQLite and Testcontainers (§6, §11). |
| **Hangfire inside the API process, on the same SQL database** | One deployable and one database, cheapest for the assessment. Always On keeps the recurring job alive (§12). | Job processing scales with, and competes with, the API. A larger deployment would move the server into its own App Service or Container App. |
| **Mock JWT login by role** | Real authentication is out of scope. Role-based authorization, tenant claims and "who did it" auditing are still exercised end to end. | Not production authentication. It would be swapped for Entra ID; the authorization code stays the same. |
| **Integration tests on real SQL Server (Testcontainers), not EF InMemory** | The behaviour worth testing lives in SQL Server: rowversion, `SEQUENCE`, unique-index races, column types, query filters (§11). | Tests need Docker and take about 40 s rather than milliseconds. The unit suites stay fast on SQLite. |
| **Frontend: signals and per-view fetching, no NgRx; runtime `config.js`** | No state in scope is shared across features. One build artifact serves every environment (§9). | Views refetch after mutations instead of updating a shared store. |
