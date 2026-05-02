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

@secure()
@description('Client secret for the Xebia app registration, used as a fallback when the federated managed-identity flow is blocked by the Xebia tenant\'s cross-tenant access policy (AADSTS700236). When set, the app overrides AzureAd:ClientCredentials[0] from appsettings.json to use this secret instead of the federated MI assertion. Empty string disables the override and the app falls back to the appsettings.json default.')
param xebiaAppClientSecret string

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
    xebiaAppClientSecret: xebiaAppClientSecret
  }
}

// Outputs consumed by azd
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_RESOURCE_GROUP string = rg.name
output SERVICE_WEB_NAME string = resources.outputs.containerAppName
output SERVICE_WEB_URI string = resources.outputs.serviceUri
