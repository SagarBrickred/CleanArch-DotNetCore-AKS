targetScope = 'resourceGroup'

@description('Environment name used for CleanArch resources.')
param environmentName string = 'dev'

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Existing SQL logical server name that will host the CleanArch database.')
param existingSqlServerName string = 'sql-order-dev-2325de'

@description('New database created for CleanArch.')
param cleanArchDatabaseName string = 'sqldb-cleanarch-dev'

@description('Existing Key Vault.')
param existingKeyVaultName string = 'kv-order-dev-2325de'

@description('Basic App Service plan SKU.')
param appServiceSkuName string = 'B1'

@description('App Service plan SKU tier.')
param appServiceSkuTier string = 'Basic'

@description('Docker image repository name.')
param containerRepository string = 'cleanarch-api'

@description('ACR SKU.')
param acrSkuName string = 'Standard'

var resourcePrefix = 'cleanarch-${environmentName}'

var appServicePlanName = 'asp-${resourcePrefix}'
var stagingAppName = 'app-${resourcePrefix}-staging'
var productionAppName = 'app-${resourcePrefix}-prod'

var acrName = 'acrcleanarchdev01'
var managedIdentityName = 'id-${resourcePrefix}'

module identity 'modules/identity.bicep' = {
  name: 'cleanarch-identity'
  params: {
    identityName: managedIdentityName
    location: location
  }
}

module keyVaultAccess 'modules/keyvault-access.bicep' = {
  name: 'cleanarch-keyvault-access'

  params: {
    keyVaultName: existingKeyVaultName
    managedIdentityPrincipalId: identity.outputs.principalId
  }
}

module acr 'modules/acr.bicep' = {
  name: 'cleanarch-acr'
  params: {
    acrName: acrName
    location: location
    skuName: acrSkuName
    managedIdentityPrincipalId: identity.outputs.principalId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'cleanarch-sql'
  params: {
    existingSqlServerName: existingSqlServerName
    databaseName: cleanArchDatabaseName
    location: location
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'cleanarch-appservice'
  params: {
    location: location
    appServicePlanName: appServicePlanName
    stagingAppName: stagingAppName
    productionAppName: productionAppName
    skuName: appServiceSkuName
    skuTier: appServiceSkuTier
    containerRegistryLoginServer: acr.outputs.loginServer
    containerRepository: containerRepository
    managedIdentityResourceId: identity.outputs.resourceId
    managedIdentityClientId: identity.outputs.clientId
    keyVaultUri: 'https://${existingKeyVaultName}.${environment().suffixes.keyvaultDns}/'
    databaseMigrateOnStartup: false
  }
}

output appServicePlanName string = appServicePlanName
output stagingAppName string = stagingAppName
output productionAppName string = productionAppName
output acrName string = acrName
output acrLoginServer string = acr.outputs.loginServer
output managedIdentityName string = managedIdentityName
output managedIdentityClientId string = identity.outputs.clientId
output cleanArchDatabaseName string = cleanArchDatabaseName
output existingSqlServerName string = existingSqlServerName
