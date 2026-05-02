targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment (used for naming resources)')
param environmentName string

@minLength(1)
@description('Primary Azure region for all resources')
param location string

@minLength(1)
@description('Full ARM resource ID of the user-assigned managed identity attached to the Container App. The MI typically lives in a different resource group (and may be in a different subscription) from the app itself; the federated credential trusting it is configured on the Xebia app registration. Microsoft.Identity.Web uses this MI for SignedAssertionFromManagedIdentity, exchanging an MI token for a confidential-client assertion against Xebia Entra.')
param userAssignedIdentityResourceId string

var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    userAssignedIdentityResourceId: userAssignedIdentityResourceId
  }
}

// Outputs consumed by azd
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_RESOURCE_GROUP string = rg.name
output SERVICE_WEB_NAME string = resources.outputs.containerAppName
output SERVICE_WEB_URI string = resources.outputs.serviceUri
