using './main.bicep'

// These values are set via `azd env set` or prompted by `azd provision`
param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'estimator-mcp')
param location       = readEnvironmentVariable('AZURE_LOCATION', 'westeurope')

// Required — must be set via:
//   azd env set USER_ASSIGNED_IDENTITY_RESOURCE_ID "<full ARM resource ID of the MI>"
// Empty string makes Bicep validation fail rather than silently strip the MI from the
// Container App.
param userAssignedIdentityResourceId = readEnvironmentVariable('USER_ASSIGNED_IDENTITY_RESOURCE_ID')
