using './main.bicep'

// These values are set via `azd env set` or prompted by `azd provision`
param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'estimator-mcp')
param location       = readEnvironmentVariable('AZURE_LOCATION', 'westeurope')

// ACS credentials — set with:
//   azd env set ACS_CONNECTION_STRING "endpoint=https://...;accesskey=..."
//   azd env set ACS_SENDER_ADDRESS "DoNotReply@your-domain.azurecomm.net"
param acsConnectionString = readEnvironmentVariable('ACS_CONNECTION_STRING', '')
param acsSenderAddress    = readEnvironmentVariable('ACS_SENDER_ADDRESS', '')
