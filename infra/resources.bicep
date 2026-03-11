@description('Name of the environment')
param environmentName string

@description('Azure region for all resources')
param location string

param tags object

// ── Naming ────────────────────────────────────────────────────────────────────

var resourceToken = toLower(uniqueString(resourceGroup().id, environmentName, location))

var acrName              = 'cr${resourceToken}'
var storageAccountName   = take('st${resourceToken}', 24)
var logAnalyticsName     = 'log-${resourceToken}'
var containerAppsEnvName = 'cae-${resourceToken}'
var containerAppName     = 'ca-${resourceToken}'
var emailServiceName     = 'email-${resourceToken}'
var communicationSvcName = 'acs-${resourceToken}'
var fileShareName        = 'estimator-data'
var storageMountName     = 'estimator-data'
var dataMountPath        = '/home/app/data'

// ── Log Analytics (required by Container Apps) ────────────────────────────────

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Container Registry ────────────────────────────────────────────────────────

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: true }
}

// ── Storage (SQLite persistence) ──────────────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: fileShareName
  parent: fileServices
  properties: {
    shareQuota: 1  // 1 GiB — sufficient for SQLite + logs
    enabledProtocols: 'SMB'
  }
}

// ── Container Apps Environment ─────────────────────────────────────────────────

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Link Azure Files share into the Container Apps Environment
resource storageMount 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: storageMountName
  parent: containerAppsEnv
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShareName
      accessMode: 'ReadWrite'
    }
  }
}

// ── Email Communication Services ──────────────────────────────────────────────

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'Europe'
  }
}

// Azure-managed domain: provisions instantly, no DNS verification required.
// Name must be exactly 'AzureManagedDomain' — this is an Azure convention.
resource emailDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  name: 'AzureManagedDomain'
  parent: emailService
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationSvcName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'Europe'
    linkedDomains: [emailDomain.id]
  }
}

var acsConnectionString = communicationService.listKeys().primaryConnectionString
var acsSenderAddress    = 'DoNotReply@${emailDomain.properties.mailFromSenderDomain}'

// ── Container App ─────────────────────────────────────────────────────────────

var acrPassword = acr.listCredentials().passwords[0].value

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        { name: 'acr-password',          value: acrPassword }
        { name: 'acs-connection-string', value: acsConnectionString }
      ]
    }
    template: {
      // NOTE: SQLite on Azure Files (SMB) fails due to missing POSIX advisory lock support.
      // Data is stored on the container's local filesystem for now (ephemeral).
      // TODO: migrate to Azure Files NFS (Premium storage + VNet) or a cloud database.
      containers: [
        {
          name: 'estimator-mcp'
          // azd replaces this image reference on first deploy
          image: 'mcr.microsoft.com/dotnet/samples:aspnetapp'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_URLS',                value: 'http://+:8080' }
            { name: 'DatabasePath',                   value: '${dataMountPath}/estimator.db' }
            { name: 'ESTIMATOR_LOGS_PATH',            value: '${dataMountPath}/logs' }
            { name: 'AzureEmailService__ConnectionString', secretRef: 'acs-connection-string' }
            { name: 'AzureEmailService__SenderAddress',   value: acsSenderAddress }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = acr.properties.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = acr.name
output containerAppName string = containerApp.name
output serviceUri string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
