// =============================================================================
// main.bicep — Orchestrates the full production environment for CleanArch.API
// Modules: networking, Log Analytics + App Insights, Key Vault, Azure SQL, ACR, AKS
// =============================================================================
targetScope = 'resourceGroup'

@description('Short environment name used in resource naming, e.g. prod, staging')
param environmentName string = 'prod'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('SQL administrator login. Password is NOT a parameter — provisioned via Key Vault-referenced deployment or AAD-only auth.')
param sqlAdminLogin string = 'cleanarchsqladmin'

@secure()
@description('SQL administrator password. Pass via --parameters sqlAdminPassword=$(secret) at deploy time — never commit this.')
param sqlAdminPassword string

@description('Your AAD object id, set as SQL AAD admin for break-glass access alongside Managed Identity auth')
param sqlAadAdminObjectId string

@description('Kubernetes version for AKS')
param kubernetesVersion string = '1.29'

@description('Node count for the AKS system pool')
param aksNodeCount int = 3

var namePrefix = 'cleanarch-${environmentName}'

module network 'modules/network.bicep' = {
  name: 'network-deployment'
  params: {
    namePrefix: namePrefix
    location: location
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    namePrefix: namePrefix
    location: location
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    namePrefix: namePrefix
    location: location
    subnetId: network.outputs.privateEndpointSubnetId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql-deployment'
  params: {
    namePrefix: namePrefix
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    aadAdminObjectId: sqlAadAdminObjectId
    subnetId: network.outputs.privateEndpointSubnetId
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr-deployment'
  params: {
    namePrefix: namePrefix
    location: location
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module aks 'modules/aks.bicep' = {
  name: 'aks-deployment'
  params: {
    namePrefix: namePrefix
    location: location
    kubernetesVersion: kubernetesVersion
    nodeCount: aksNodeCount
    subnetId: network.outputs.aksSubnetId
    acrId: acr.outputs.acrId
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Grant the AKS-managed identity (used by Key Vault CSI driver / Workload Identity) "get" access on secrets.
module kvAccessPolicy 'modules/keyvault-access.bicep' = {
  name: 'kv-access-deployment'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    principalId: aks.outputs.kubeletIdentityObjectId
  }
}

// Store the SQL connection string as a Key Vault secret — read by the API at startup via AddAzureKeyVault().
module sqlSecret 'modules/sql-secret.bicep' = {
  name: 'sql-secret-deployment'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    sqlServerFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.sqlDatabaseName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
  }
}

output aksClusterName string = aks.outputs.clusterName
output acrLoginServer string = acr.outputs.loginServer
output keyVaultUri string = keyVault.outputs.keyVaultUri
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId
