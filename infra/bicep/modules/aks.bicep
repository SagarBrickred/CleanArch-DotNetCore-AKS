param namePrefix string
param location string
param kubernetesVersion string
param nodeCount int
param subnetId string
param acrId string
param logAnalyticsWorkspaceId string

resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: '${namePrefix}-aks'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    kubernetesVersion: kubernetesVersion
    dnsPrefix: '${namePrefix}-aks'
    enableRBAC: true
    disableLocalAccounts: true         // AAD-only cluster access — no static kubeconfig admin creds
    aadProfile: {
      managed: true
      enableAzureRBAC: true
    }
    oidcIssuerProfile: { enabled: true }        // required for Workload Identity federation
    securityProfile: {
      workloadIdentity: { enabled: true }
      defender: { securityMonitoring: { enabled: true } }
    }
    networkProfile: {
      networkPlugin: 'azure'
      networkPolicy: 'azure'          // enforces the NetworkPolicy manifests applied to the cluster
      loadBalancerSku: 'standard'
      serviceCidr: '10.20.0.0/16'
      dnsServiceIP: '10.20.0.10'
    }
    apiServerAccessProfile: {
      enablePrivateCluster: true      // API server has no public endpoint
    }
    addonProfiles: {
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logAnalyticsWorkspaceId
        }
      }
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
    }
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        count: nodeCount
        vmSize: 'Standard_D4s_v5'
        osType: 'Linux'
        vnetSubnetID: subnetId
        availabilityZones: ['1', '2', '3']
        enableAutoScaling: true
        minCount: 3
        maxCount: 6
        maxPods: 60
      }
    ]
  }
}

// AcrPull role for the kubelet identity — lets AKS nodes pull images without embedding registry creds.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acrId, aks.id, acrPullRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

output clusterName string = aks.name
output kubeletIdentityObjectId string = aks.properties.identityProfile.kubeletidentity.objectId
output oidcIssuerUrl string = aks.properties.oidcIssuerProfile.issuerURL
