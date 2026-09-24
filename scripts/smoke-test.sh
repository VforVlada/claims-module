#!/usr/bin/env bash
# Post-deployment smoke tests (testing plan section 7). Non-destructive except S-05, which
# files one claim as the mock handler — point it at a smoke-test organization in shared envs.
#
#   API_URL=https://claims-api.example.net FRONTEND_URL=https://claims.example.net ./scripts/smoke-test.sh
#
# SKIP_E2E=1 skips S-05 (the Playwright happy path) when no browser is available.
set -euo pipefail

API_URL="${API_URL:?set API_URL, e.g. https://claims-api.example.net}"
FRONTEND_URL="${FRONTEND_URL:?set FRONTEND_URL, e.g. https://claims.example.net}"
API_URL="${API_URL%/}"
FRONTEND_URL="${FRONTEND_URL%/}"

failures=0
pass() { echo "  PASS  $1"; }
fail() { echo "  FAIL  $1"; failures=$((failures + 1)); }

# Retries cover a cold start right after deploy (App Service warm-up, SQL serverless resume).
fetch() { curl --silent --show-error --fail --retry 5 --retry-delay 6 --retry-all-errors --max-time 30 "$@"; }

echo "Smoke testing API=$API_URL FRONTEND=$FRONTEND_URL"

# S-01: health, including the DB and Blob checks.
if health=$(fetch "$API_URL/health") && [[ "$health" == "Healthy" ]]; then
  pass "S-01 /health → Healthy"
else
  fail "S-01 /health (got: ${health:-no response})"
fi

# S-02: Swagger JSON loads and lists the core endpoints.
if swagger=$(fetch "$API_URL/swagger/v1/swagger.json"); then
  missing=()
  for path in "/api/claims" "/api/claims/{id}/status" "/api/claims/{claimId}/reserves" "/api/reference/claim-statuses" "/api/auth/mock-login"; do
    grep -qF "\"$path\"" <<<"$swagger" || missing+=("$path")
  done
  if [[ ${#missing[@]} -eq 0 ]]; then pass "S-02 Swagger lists all core endpoints"; else fail "S-02 Swagger is missing: ${missing[*]}"; fi
else
  fail "S-02 Swagger JSON did not load"
fi

# S-03: the SPA is served (index.html with the Angular root element).
if page=$(fetch "$FRONTEND_URL/") && grep -q "<app-root" <<<"$page"; then
  pass "S-03 frontend serves the Angular shell"
else
  fail "S-03 frontend did not serve the Angular shell"
fi

# S-04: reference data is seeded, which proves migrations and seed ran against this database.
token=$(fetch -X POST -H "Content-Type: application/json" -d '{"role":"handler"}' "$API_URL/api/auth/mock-login" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p' || true)
if [[ -z "$token" ]]; then
  fail "S-04 could not obtain a mock-login token"
else
  codes=$(fetch -H "Authorization: Bearer $token" "$API_URL/api/reference/cause-of-loss-codes" || true)
  statuses=$(fetch -H "Authorization: Bearer $token" "$API_URL/api/reference/claim-statuses" || true)
  if grep -q '"COLL"' <<<"$codes" && grep -q '"allowedNextStatuses"' <<<"$statuses"; then
    pass "S-04 reference data is seeded"
  else
    fail "S-04 reference data missing (codes: ${codes:0:80}…)"
  fi
fi

# S-05: the E2E-01 happy path against the deployed URLs.
if [[ "${SKIP_E2E:-0}" == "1" ]]; then
  echo "  SKIP  S-05 (SKIP_E2E=1)"
else
  if (cd "$(dirname "$0")/../frontend/claims-module-ui" && E2E_BASE_URL="$FRONTEND_URL" E2E_API_URL="$API_URL" npm run --silent e2e:smoke); then
    pass "S-05 E2E-01 happy path"
  else
    fail "S-05 E2E-01 happy path"
  fi
fi

if [[ $failures -gt 0 ]]; then
  echo "Smoke tests FAILED ($failures check(s))."
  exit 1
fi
echo "Smoke tests passed."
