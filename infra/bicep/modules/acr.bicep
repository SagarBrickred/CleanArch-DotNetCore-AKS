param namePrefix string
param location string
param logAnalyticsWorkspaceId string

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: replace('${namePrefix}acr', '-', '')
  location: location
  sku: { name: 'Premium' }    // Premium required for private endpoints + geo-replication + content trust
  properties: {
    adminUserEnabled: false   // AKS pulls via Managed Identity / AcrPull role, never the admin account
    publicNetworkAccess: 'Disabled'
    policies: {
      quarantinePolicy: { status: 'enabled' }     // images held until vulnerability scan clears
      trustPolicy: { status: 'enabled', type: 'Notary' }
      retentionPolicy: { status: 'enabled', days: 30 }
    }
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: acr
  name: 'acr-diagnostics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'ContainerRegistryRepositoryEvents', enabled: true }
      { category: 'ContainerRegistryLoginEvents', enabled: true }
    ]
  }
}

output acrId string = acr.id
output loginServer string = acr.properties.loginServer
