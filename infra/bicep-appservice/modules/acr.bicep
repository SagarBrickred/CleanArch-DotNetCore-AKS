
param acrName string
param location string
param skuName string
param managedIdentityPrincipalId string

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location

  sku: {
    name: skuName
  }

  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(
    acr.id,
    managedIdentityPrincipalId,
    '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  )

  scope: acr

  properties: {
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

output acrId string = acr.id
output loginServer string = acr.properties.loginServer

