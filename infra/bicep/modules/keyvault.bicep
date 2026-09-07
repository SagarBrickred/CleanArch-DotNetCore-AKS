param namePrefix string
param location string
param subnetId string

var keyVaultName = take('kv-${replace(namePrefix, '-', '')}', 24)   // Key Vault names are capped at 24 chars

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true          // RBAC over legacy access policies — auditable via Azure AD
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true            // prevents accidental/malicious permanent deletion of secrets
    publicNetworkAccess: 'Disabled'        // secrets only reachable via the private endpoint below
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: '${namePrefix}-kv-pe'
  location: location
  properties: {
    subnet: { id: subnetId }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-kv-plsc'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: ['vault']
        }
      }
    ]
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultId string = keyVault.id
