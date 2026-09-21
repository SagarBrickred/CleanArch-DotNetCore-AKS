param namePrefix string
param location string
param adminLogin string

@secure()
param adminPassword string
param aadAdminObjectId string
param subnetId string
param logAnalyticsWorkspaceId string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: '${namePrefix}-sqlsrv'
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'       // reachable only via private endpoint — no public SQL surface
    administrators: {
      administratorType: 'ActiveDirectory'
      login: 'sql-aad-admin'
      sid: aadAdminObjectId
      azureADOnlyAuthentication: false     // set true once all connections are migrated to Managed Identity / AAD tokens
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: '${namePrefix}-db'
  location: location
  sku: {
    name: 'GP_Gen5_2'      // General Purpose, 2 vCores — right-size per workload; zone-redundant available on GP/BC
    tier: 'GeneralPurpose'
  }
  properties: {
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Geo'   // geo-redundant backups for DR
  }
}

// Point-in-time restore is on by default (7-35 days configurable); this makes the retention explicit.
resource backupPolicy 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01' = {
  parent: sqlDatabase
  name: 'default'
  properties: {
    retentionDays: 35
  }
}

resource longTermBackup 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2023-08-01' = {
  parent: sqlDatabase
  name: 'default'
  properties: {
    weeklyRetention: 'P12W'
    monthlyRetention: 'P12M'
    yearlyRetention: 'P5Y'
    weekOfYear: 1
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: '${namePrefix}-sql-pe'
  location: location
  properties: {
    subnet: { id: subnetId }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-sql-plsc'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource auditingSettings 'Microsoft.Sql/servers/auditingSettings@2023-08-01' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: sqlDatabase
  name: 'sql-diagnostics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'SQLInsights', enabled: true }
      { category: 'Errors', enabled: true }
      { category: 'DatabaseWaitStatistics', enabled: true }
      { category: 'Timeouts', enabled: true }
      { category: 'Blocks', enabled: true }
      { category: 'Deadlocks', enabled: true }
    ]
    metrics: [
      { category: 'Basic', enabled: true }
    ]
  }
}

// Advanced Threat Protection — alerts on SQL injection, anomalous access patterns, brute force.
resource threatProtection 'Microsoft.Sql/servers/securityAlertPolicies@2023-08-01' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    emailAccountAdmins: true
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
