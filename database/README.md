# Database scripts

EF Core migrations (`src/ClaimsModule.Persistence/Migrations`) are the source of truth for both
schema and seed data. These scripts are exports of them, for DBAs or for environments that are
not migrated through EF.

| File | What it is | Regenerate with |
|---|---|---|
| `ClaimsModule.migrations.sql` | Idempotent script of every migration: schema, seed data and data repairs. Safe to re-run. | `dotnet ef migrations script --idempotent --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API -o database/ClaimsModule.migrations.sql` |
| `seed-reference-data.sql` | Reference and simulated policy data only: cause-of-loss codes, status transitions, policies, coverages. Inserts rows whose Id is missing. | Run `generate-seed.sql` against a migrated database (command in its header) |
| `generate-seed.sql` | Scripts the seeded tables' current rows as idempotent inserts, so the seed script can't drift from the migrations. | — |

Run them with `sqlcmd -I` (QUOTED_IDENTIFIER ON), which the filtered unique index on `ClaimAuditLogs.IdempotencyKey` requires:

```bash
sqlcmd -S <server> -d ClaimsModule -I -i database/ClaimsModule.migrations.sql
sqlcmd -S <server> -d ClaimsModule -I -i database/seed-reference-data.sql
```
