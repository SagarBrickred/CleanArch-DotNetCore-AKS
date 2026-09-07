@description('Naming prefix for all resources in this module')
param namePrefix string
param location string

// Hub-and-spoke-lite: a single VNet with subnets for AKS nodes and PaaS private endpoints,
// each protected by an NSG. In a larger org this VNet would peer to a hub with a firewall.
resource vnet 'Microsoft.Network/virtualNetworks@2023-09-01' = {
  name: '${namePrefix}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.10.0.0/16']
    }
    subnets: [
      {
        name: 'aks-subnet'
        properties: {
          addressPrefix: '10.10.0.0/20'
          networkSecurityGroup: { id: aksNsg.id }
        }
      }
      {
        name: 'private-endpoint-subnet'
        properties: {
          addressPrefix: '10.10.16.0/24'
          networkSecurityGroup: { id: peNsg.id }
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource aksNsg 'Microsoft.Network/networkSecurityGroups@2023-09-01' = {
  name: '${namePrefix}-aks-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'DenyAllInboundInternet'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

resource peNsg 'Microsoft.Network/networkSecurityGroups@2023-09-01' = {
  name: '${namePrefix}-pe-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'DenyAllInboundInternet'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

output aksSubnetId string = vnet.properties.subnets[0].id
output privateEndpointSubnetId string = vnet.properties.subnets[1].id
