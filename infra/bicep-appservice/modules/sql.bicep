
param existingSqlServerName string
param databaseName string
param location string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: existingSqlServerName
}

resource cleanArchDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: databaseName
  location: location

  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
  }

  properties: {
    requestedBackupStorageRedundancy: 'Local'
  }
}

output databaseId string = cleanArchDatabase.id
output databaseName string = cleanArchDatabase.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

