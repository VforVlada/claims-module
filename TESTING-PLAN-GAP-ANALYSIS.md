# Testing Plan — Status

Source plan: `DICEUS Claims Module — Testing Plan.docx` (Sep 22, 2026). Last updated 2026-09-23.

Every section of the plan is now implemented. Test names and summaries carry the plan's IDs, so any requirement can be traced with a grep, e.g. `grep -rn "I-JOB-03" tests frontend`. See `ARCHITECTURE.md` §11 for how the layers fit together.

## Results (local run, 2026-09-23)

| Suite | Result |
|---|---|
| Domain unit | 55 passed |
| Application unit | 293 passed |
| Infrastructure unit | 26 passed |
| Architecture (NetArchTest) | 8 passed |
| Integration (Testcontainers SQL Server 2022 + Azurite) | 87 passed |
| Frontend unit (Karma, Material harnesses) | 140 passed; ESLint clean |
| E2E (Playwright, Docker stack) | 9 passed |
| Smoke script (local stack) | S-01..S-05 passed |
| Coverage, Domain + Application | 96.1% lines, 83.5% branches (CI gate: 90% lines) |

**Not yet run: the Azure parts of sections 7 and 8.** No Azure environment exists yet, so the CI pipeline's migrate/deploy/smoke stages have never executed. The same smoke script does pass against a local stack.

## Coverage by section

| Section | Status |
|---|---|
| 2 Environments & data | Testcontainers + Respawn per test. Seed data covers Org A/B, an inactive code, an Org B code, active/expired/narrow-window policies, and an Org B user. `ClaimBuilder`/`ReserveBuilder` exist. Reserve decisions use `IDateTimeProvider`. |
| 3.1 Claim creation | U-C-01..11 |
| 3.2 State machine | 49 pairs × 3 roles theory at unit level, plus the same check against the real seeded table. U-W-03 is backed by `Workflow:AllowTrivialClaimClosure`. |
| 3.3 Reserve authority | U-R-01..23, including the exact 4-decimal boundaries |
| 3.4 Handlers & pipeline | Command→validator reflection test; AutoMapper `AssertConfigurationIsValid`; UoW commit-once / rollback; post-commit event dispatch; GL enqueue per tier |
| 4.1 Persistence | I-DB-01..10 |
| 4.2 API contracts | I-API-01..14 |
| 4.3 Hangfire | I-JOB-01..11, plus a real `BackgroundJobServer` wiring test |
| 4.4 Storage | I-ST-01..06 against both Azurite and LocalFileSystem |
| 5 Architecture | A-01..A-05, plus naming conventions |
| 6.1 Frontend unit | F-01..F-13; ESLint `no-restricted-imports` bans `HttpClient` outside `*.service.ts` (UI-02) |
| 6.2 E2E | E2E-01..07 (E2E-01 tagged `@smoke`) |
| 7 Smoke | `scripts/smoke-test.sh` (S-01..S-05) |
| 8 CI | `.github/workflows/ci.yml`: build → backend + frontend tests (coverage gate) → migrate → deploy → smoke |

## Defects found and fixed while implementing the plan

These are production bugs, not test-only changes. The earlier round of fixes (reserve authority, GL race, claim-creation validation, tenant filter, Swagger/CORS/health) is in git history.

1. **Reserve history was invisible after the tenant filter was added.** `ReserveHistory` rows were created without an `OrganizationEntityId`, so the filter hid them from everyone. As a result:
   - approving failed with "history does not belong to component"
   - adjusting threw a duplicate `ChangeSequence` (500)
   - balances showed 0

   Fixed at creation, with a backfill migration (`BackfillMissingOrganizationIds`) and a `SaveChanges` guard that rejects tenant-owned rows with an empty organization.
2. **Background jobs saw no rows at all.** Hangfire jobs have no HTTP user, so the tenant filter resolved to `Guid.Empty`. As a result:
   - GL posting never happened
   - the SLA job found nothing
   - job-written audit rows were stamped with `Guid.Empty`

   The jobs now read past the filter, and audit rows inherit the claim's organization.
3. **The SLA job was never scheduled.** It was registered with DI, but there was no `RecurringJob.AddOrUpdate`. Fixed with `RecurringJobRegistration` (`*/15 * * * *`).
4. **The transition graph contradicted the plan.**
   - Draft → Closed was seeded; U-W-01 says it must be rejected.
   - Every role could perform every transition, so WF-05's 403 was impossible. Reopening is now Supervisor/Manager only, and a role failure returns 403, distinct from a not-in-workflow 409.
   - The domain accepted a same-status "transition".
5. **Reject had no authority check.** A Supervisor could reject a Manager-tier change. It now uses the same tier rule as approve.
6. **The error contract didn't match the plan.** Validation errors were 422 with a custom body, and unknown routes returned an empty 404. Errors are now RFC 7807 ProblemDetails, with 400 for validation and 409 for invalid transitions (convention in `ARCHITECTURE.md` §4).
7. **The policy-period warning (BR-C-02) was never audited.** It is now raised as a domain event and audited post-commit.
8. **Two storage bugs.**
   - Blob paths were doubled (`claim-documents/claim-documents/...`).
   - Two uploads with the same file name crashed on Azure and silently overwrote locally. Paths are now `{org}/{claim}/{unique}-{file}`.
9. **A file just over 50 MB returned 500 instead of 400.** The request-size cap equalled the file limit, so multipart overhead tripped it first.
10. **Domain referenced MediatR** (`DomainEvent : INotification`), violating A-01. Events are now plain records, wrapped by `DomainEventNotification<T>` in Application.
11. **`ApproveReserveCommand` had no validator.** This was caught by the plan's reflection test.
12. **The deployed frontend would call `localhost`.** The API origin was a hard-coded constant. It now comes from a runtime `config.js` that the deploy step writes.
13. **The integration harness had never worked.** Its connection-string override applied too late, so migrations ran against the developer's LocalDB and every test failed.
14. **Paging was unstable.** List ordering had no tiebreaker, so rows created in one batch could repeat or be skipped across pages.
15. **Existing claims broke the claim list.** Claims created before the 7-digit claim-number change (e.g. `CLM-2026-000051`) failed validation on load, so `GET /api/claims` returned 500 on any existing database. Found by running the Docker stack against its persisted volume. Legacy 6-digit numbers now load; new numbers are still 7-digit.
16. **The $10M aggregate cap (BR-R-07) could be exceeded on approval.** Pending amounts don't count toward the cap, and approval never re-checked it. Approval now re-checks it; crossing it needs an explicit Manager override, which flags the claim and is audited. The unused SLA event path and `InvalidStatusTransitionException` were removed.

## Open items

See also the requirements review against the assessment brief, which added the SLA flag, the GL journal wording, reserve previous/new amounts with reasons, document types, Azure provisioning (`infra/`) and SQL seed scripts (`database/`). Details are in `ARCHITECTURE.md` §12–13.


- **Azure.** Provision the environment and set the secrets/variables listed at the top of `ci.yml`. The migrate/deploy/smoke stages then run on every push to `main`.
