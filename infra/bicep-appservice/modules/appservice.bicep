
param location string

param appServicePlanName string
param stagingAppName string
param productionAppName string

param skuName string
param skuTier string

param containerRegistryLoginServer string
param containerRepository string

param managedIdentityResourceId string
param managedIdentityClientId string

param keyVaultUri string

param databaseMigrateOnStartup bool

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location

  kind: 'linux'

  sku: {
    name: skuName
    tier: skuTier
  }

  properties: {
    reserved: true
  }
}

resource stagingApp 'Microsoft.Web/sites@2023-12-01' = {
  name: stagingAppName
  location: location

  kind: 'app,linux,container'

  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }

  properties: {
    serverFarmId: appServicePlan.id

    httpsOnly: true

    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerRegistryLoginServer}/${containerRepository}:latest'

      alwaysOn: true

      healthCheckPath: '/health/live'
      
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: managedIdentityClientId


      appSettings: [
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Staging'
        }
        {
          name: 'KeyVault__Uri'
          value: keyVaultUri
        }
        {
          name: 'Database__MigrateOnStartup'
          value: string(databaseMigrateOnStartup)
        }
      ]
    }
  }
}

resource productionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: productionAppName
  location: location

  kind: 'app,linux,container'

  dependsOn: [
    stagingApp
  ]

  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }

  properties: {
    serverFarmId: appServicePlan.id

    httpsOnly: true

    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerRegistryLoginServer}/${containerRepository}:latest'

      alwaysOn: true

      healthCheckPath: '/health/live'

      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: managedIdentityClientId

      appSettings: [
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'KeyVault__Uri'
          value: keyVaultUri
        }
        {
          name: 'Database__MigrateOnStartup'
          value: string(databaseMigrateOnStartup)
        }
      ]
    }
  }
}

output appServicePlanId string = appServicePlan.id
output stagingAppId string = stagingApp.id
output productionAppId string = productionApp.id
