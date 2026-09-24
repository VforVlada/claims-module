// Claims Module — Azure infrastructure (brief §3.8).
//
//   API        App Service (Linux, .NET 9), system-assigned managed identity
//   Frontend   Azure Static Web App (Free)
//   Database   Azure SQL Database
//   Documents  Storage account, private "claim-documents" container (SAS URLs for download)
//   Secrets    Key Vault; the App Service reads them through Key Vault references
//
// Deploy with infra/deploy.sh, which also prints the GitHub secrets/variables the CI pipeline needs.

@description('Short lowercase prefix for resource names, e.g. "claimsdemo". 3-11 characters.')
@minLength(3)
@maxLength(11)
param namePrefix string

param location string = resourceGroup().location

@description('Static Web Apps is only offered in some regions; this one hosts the SPA.')
param staticWebAppLocation string = 'westeurope'

param sqlAdminLogin string = 'claimsadmin'

@secure()
param sqlAdminPassword string

@description('HMAC key for the mock JWTs (32+ characters).')
@secure()
@minLength(32)
param jwtSigningKey string

@description('Azure SQL SKU. Basic (5 DTU) is enough for the assessment.')
param sqlSkuName string = 'Basic'

@description('App Service plan SKU. B1 supports Always On, which keeps the Hangfire server running.')
param appServiceSkuName string = 'B1'

var suffix = uniqueString(resourceGroup().id)
var apiName = '${namePrefix}-api-${suffix}'
var containerName = 'claim-documents'

// ---------- Storage (Blob) ----------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take(toLower('${namePrefix}st${suffix}'), 24)
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: { publicAccess: 'None' }
}

// ---------- SQL ----------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${namePrefix}-sql-${suffix}'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// 0.0.0.0 - 0.0.0.0 is Azure's "allow Azure services" rule (lets the App Service connect).
// The CI migrate job opens a temporary rule for its own runner IP and removes it afterwards.
resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ClaimsModule'
  location: location
  sku: { name: sqlSkuName }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

// ---------- Frontend ----------
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${namePrefix}-web-${suffix}'
  location: staticWebAppLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// ---------- API ----------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namePrefix}-plan-${suffix}'
  location: location
  sku: { name: appServiceSkuName }
  kind: 'linux'
  properties: { reserved: true }
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: apiName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: appServiceSkuName != 'F1' // Hangfire's recurring SLA job needs the process kept alive
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
    }
  }
}

// ---------- Key Vault ----------
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('${namePrefix}-kv-${suffix}', 24)
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource secretSql 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'ClaimsDatabase'
  properties: { value: sqlConnectionString }
}

resource secretStorage 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'StorageConnectionString'
  properties: { value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
}

resource secretJwt 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'JwtSigningKey'
  properties: { value: jwtSigningKey }
}

// "Key Vault Secrets User" for the API's managed identity.
resource apiCanReadSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, api.id, 'kv-secrets-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: api
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    ConnectionStrings__ClaimsDatabase: '@Microsoft.KeyVault(SecretUri=${secretSql.properties.secretUri})'
    Jwt__SigningKey: '@Microsoft.KeyVault(SecretUri=${secretJwt.properties.secretUri})'
    Storage__Provider: 'AzureBlob'
    Storage__AzureBlobConnectionString: '@Microsoft.KeyVault(SecretUri=${secretStorage.properties.secretUri})'
    Storage__AzureBlobContainerName: containerName
    Cors__AllowedOrigins__0: 'https://${staticWebApp.properties.defaultHostname}'
  }
  dependsOn: [ apiCanReadSecrets ]
}

output apiName string = api.name
output apiUrl string = 'https://${api.properties.defaultHostName}'
output frontendUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output staticWebAppName string = staticWebApp.name
output sqlServerName string = sqlServer.name
output keyVaultName string = vault.name
output storageAccountName string = storage.name
