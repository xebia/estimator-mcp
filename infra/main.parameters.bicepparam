using './main.bicep'

// These values are set via `azd env set` or prompted by `azd provision`
param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'estimator-mcp')
param location       = readEnvironmentVariable('AZURE_LOCATION', 'westeurope')
