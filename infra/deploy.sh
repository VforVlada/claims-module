#!/usr/bin/env bash
# Provisions the Claims Module on Azure (infra/main.bicep) and prints everything the CI
# pipeline needs. Idempotent: re-running updates the same resources.
#
#   az login
#   NAME_PREFIX=claimsdemo RESOURCE_GROUP=claims-module-rg LOCATION=westeurope ./infra/deploy.sh
#
# Optional:
#   SQL_ADMIN_PASSWORD / JWT_SIGNING_KEY   supply your own (otherwise generated and printed once)
#   SET_GITHUB_SECRETS=1                   also write the secrets/variables with the GitHub CLI (gh)
set -euo pipefail

NAME_PREFIX="${NAME_PREFIX:-claimsdemo}"
RESOURCE_GROUP="${RESOURCE_GROUP:-claims-module-rg}"
LOCATION="${LOCATION:-westeurope}"
SQL_ADMIN_LOGIN="${SQL_ADMIN_LOGIN:-claimsadmin}"
# Azure SQL requires upper, lower, digit and symbol; the suffix guarantees all four.
SQL_ADMIN_PASSWORD_WAS_SET=${SQL_ADMIN_PASSWORD:+1}
SQL_ADMIN_PASSWORD="${SQL_ADMIN_PASSWORD:-$(openssl rand -base64 24 | tr -d '/+=')Aa1!}"
JWT_SIGNING_KEY="${JWT_SIGNING_KEY:-$(openssl rand -base64 48 | tr -d '\n')}"

cd "$(dirname "$0")/.."
az account show > /dev/null || { echo "Run 'az login' first."; exit 1; }
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

echo "==> Resource group $RESOURCE_GROUP ($LOCATION)"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "==> Deploying infra/main.bicep (takes a few minutes)"
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --name claims-module \
  --template-file infra/main.bicep \
  --parameters namePrefix="$NAME_PREFIX" sqlAdminLogin="$SQL_ADMIN_LOGIN" \
               sqlAdminPassword="$SQL_ADMIN_PASSWORD" jwtSigningKey="$JWT_SIGNING_KEY" \
  --output none

output() { az deployment group show -g "$RESOURCE_GROUP" -n claims-module --query "properties.outputs.$1.value" -o tsv; }
API_NAME=$(output apiName)
API_URL=$(output apiUrl)
FRONTEND_URL=$(output frontendUrl)
SWA_NAME=$(output staticWebAppName)
SQL_SERVER=$(output sqlServerName)

SWA_TOKEN=$(az staticwebapp secrets list --name "$SWA_NAME" --resource-group "$RESOURCE_GROUP" --query properties.apiKey -o tsv)
SQL_CONNECTION="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=ClaimsModule;User ID=${SQL_ADMIN_LOGIN};Password=${SQL_ADMIN_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

echo "==> Service principal for GitHub Actions (Contributor on $RESOURCE_GROUP only)"
AZURE_CREDENTIALS=$(az ad sp create-for-rbac --name "${NAME_PREFIX}-claims-ci" --role Contributor \
  --scopes "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}" --json-auth --only-show-errors)

if [[ "${SET_GITHUB_SECRETS:-0}" == "1" ]]; then
  echo "==> Writing GitHub secrets and variables with gh"
  gh secret set AZURE_CREDENTIALS --body "$AZURE_CREDENTIALS"
  gh secret set AZURE_SQL_CONNECTION_STRING --body "$SQL_CONNECTION"
  gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --body "$SWA_TOKEN"
  gh variable set AZURE_RESOURCE_GROUP --body "$RESOURCE_GROUP"
  gh variable set AZURE_SQL_SERVER_NAME --body "$SQL_SERVER"
  gh variable set AZURE_WEBAPP_NAME --body "$API_NAME"
  gh variable set API_URL --body "$API_URL"
  gh variable set FRONTEND_URL --body "$FRONTEND_URL"
fi

cat <<EOF

Provisioned.
  API (Swagger):  ${API_URL}/swagger
  Frontend:       ${FRONTEND_URL}

GitHub → Settings → Secrets and variables → Actions$( [[ "${SET_GITHUB_SECRETS:-0}" == "1" ]] && echo " (already written)" ):
  secret    AZURE_CREDENTIALS                 (the service-principal JSON printed below)
  secret    AZURE_SQL_CONNECTION_STRING       ${SQL_CONNECTION%%Password=*}Password=***
  secret    AZURE_STATIC_WEB_APPS_API_TOKEN   (az staticwebapp secrets list -n ${SWA_NAME} -g ${RESOURCE_GROUP})
  variable  AZURE_RESOURCE_GROUP              ${RESOURCE_GROUP}
  variable  AZURE_SQL_SERVER_NAME             ${SQL_SERVER}
  variable  AZURE_WEBAPP_NAME                 ${API_NAME}
  variable  API_URL                           ${API_URL}
  variable  FRONTEND_URL                      ${FRONTEND_URL}

Then run the CI workflow (Actions → CI → Run workflow, or push to main): it migrates the
database, deploys both apps and smoke-tests them.
EOF

if [[ "${SET_GITHUB_SECRETS:-0}" != "1" ]]; then
  echo
  echo "AZURE_CREDENTIALS (paste as the secret value, then keep it private):"
  echo "$AZURE_CREDENTIALS"
  echo
  echo "AZURE_STATIC_WEB_APPS_API_TOKEN: $SWA_TOKEN"
fi

if [[ -z "${SQL_ADMIN_PASSWORD_WAS_SET:-}" ]]; then
  echo
  echo "Generated SQL admin password (store it now, it is not saved anywhere else): ${SQL_ADMIN_PASSWORD}"
fi
