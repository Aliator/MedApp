// MedApp's Azure infrastructure, in one resource group (deploy/azure/README.md):
//   - a virtual network: AKS's nodes, and the private endpoint for Azure SQL
//   - AKS: Azure CNI Overlay with Cilium (network policies enforced), workload identity, Entra ID sign-in only
//   - Azure Container Registry: the API's image, pulled by AKS with its own identity
//   - Azure SQL: serverless, reachable only through its private endpoint, with a lock against deleting it
//   - Key Vault: the database, first admin, backplane and email secrets, read by External Secrets Operator, and the
//     key that wraps the API's data protection keys
//   - Azure Communication Services Email: an Azure-managed sender domain and the SMTP relay the API sends through,
//     signing in as a Microsoft Entra app made by hand (README.md, Email)
//   - managed identities: one for AKS's control plane (its subnet), one for External Secrets (Key Vault), one for
//     the API (the data protection key), one for GitHub Actions (pushing to the registry)
// Deploy it with deploy.sh, which runs what-if first. Then bootstrap.sh sets up the cluster.

targetScope = 'resourceGroup'

@description('Short name for this environment, used in resource names, e.g. prod: 1 to 8 lower-case letters and digits only (the registry\'s name allows only letters and digits, and the SQL server\'s only lower case). Anything else fails the deployment, with a message saying so.')
@minLength(1)
@maxLength(8)
param environmentName string

param location string = resourceGroup().location

@description('The Entra ID group of MedApp\'s Azure admins: cluster admins, and the SQL server\'s Entra admin.')
param adminGroupObjectId string

@description('That group\'s display name, as Azure SQL shows its Entra admin.')
param adminGroupName string

@description('The GitHub repository whose Publish workflow pushes images (owner/name), on its main branch.')
param githubRepository string = 'Aliator/MedApp'

@description('AKS node size. Two D2s v5 nodes are about the smallest that run MedApp and the cluster services well.')
param nodeVmSize string = 'Standard_D2s_v5'

@minValue(1)
param nodeMinCount int = 2

@minValue(1)
param nodeMaxCount int = 4

@description('Free has no uptime SLA; Standard has one, for production.')
@allowed(['Free', 'Standard'])
param aksTier string = 'Free'

@description('Minutes idle before the serverless database pauses (-1: never). A paused database takes about a minute to wake.')
param sqlAutoPauseDelay int = 60

@description('The SQL server\'s own login, which the app connects as (SQL authentication).')
param sqlAdminLogin string = 'medapp'

@secure()
param sqlAdminPassword string

@description('The first admin account\'s password (username admin), made by the migrations Job on an empty database.')
@secure()
param firstAdminPassword string

@description('Valkey\'s password: the SignalR backplane, inside the cluster.')
@secure()
param backplanePassword string

@description('Where Azure Communication Services keeps email data at rest: a geography, not a region.')
param emailDataLocation string = 'UK'

@description('The application (client) ID of the Microsoft Entra app the API signs in to the SMTP relay as (README.md, Email). Empty: no SMTP user name, so email can\'t be sent yet.')
param smtpAppClientId string = ''

@description('The object ID of that app\'s service principal (az ad sp show --id <client ID> --query id), which gets the role to send.')
param smtpAppPrincipalId string = ''

@description('One of that app\'s client secrets: the SMTP password.')
@secure()
param smtpPassword string = ''

// The environment's name, checked: Bicep has no pattern decorator, so any character that isn't a lower-case letter
// or a digit fails the deployment here, with a clear message, rather than later on one resource's name.
var environmentChars = map(range(0, length(environmentName)), i => substring(environmentName, i, 1))
var badEnvironmentChars = filter(environmentChars, c => !contains('abcdefghijklmnopqrstuvwxyz0123456789', c))
var environment = empty(badEnvironmentChars)
  ? environmentName
  : fail('environmentName must be lower-case letters and digits only, but "${environmentName}" has "${join(badEnvironmentChars, '')}"')

// The SMTP app is set up with both its IDs, or not at all.
var smtpConfigured = empty(smtpAppClientId) && empty(smtpAppPrincipalId)
  ? false
  : !empty(smtpAppClientId) && !empty(smtpAppPrincipalId)
      ? true
      : fail('Set both smtpAppClientId and smtpAppPrincipalId, or neither (README.md, Email).')

// Names: <type>-medapp-<environment>, except where Azure needs them globally unique and alphanumeric.
var suffix = uniqueString(resourceGroup().id)
var names = {
  vnet: 'vnet-medapp-${environment}'
  aks: 'aks-medapp-${environment}'
  acr: 'crmedapp${environment}${suffix}'
  sql: 'sql-medapp-${environment}-${suffix}'
  // Key Vault names are at most 24 characters: 'kv-medapp-' (10) + the environment (at most 8) + '-' (1) + 5 = 24.
  keyVault: 'kv-medapp-${environment}-${take(suffix, 5)}'
  aksIdentity: 'id-medapp-${environment}-aks'
  externalSecretsIdentity: 'id-medapp-${environment}-external-secrets'
  apiIdentity: 'id-medapp-${environment}-api'
  githubIdentity: 'id-medapp-${environment}-github'
  // Communication Services' names are global (<name>.communication.azure.com).
  emailService: 'ecs-medapp-${environment}-${suffix}'
  communication: 'acs-medapp-${environment}-${suffix}'
  // The SMTP user name is global too, so it carries the suffix.
  smtpUsername: 'medapp-${environment}-${suffix}'
}

// The API's Kubernetes service account (the chart's <release>-api, in the medapp namespace that bootstrap.sh makes),
// which its managed identity is federated with.
var apiServiceAccount = 'system:serviceaccount:medapp:medapp-api'

// Built-in roles.
var roles = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  acrPush: '8311e382-0749-4cb8-b61a-304f252e45ec'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  // Read a key, and wrap and unwrap with it: nothing else.
  keyVaultCryptoServiceEncryptionUser: 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
  networkContributor: '4d97b98b-1d4f-4787-a291-c67834d212e7'
  aksRbacClusterAdmin: 'b1ff04bb-8a4e-4dc4-8eb5-8693973ce19b'
}

// ---------------------------------------------------------------------------------------------------- Network

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: names.vnet
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.40.0.0/16']
    }
    subnets: [
      {
        name: 'aks'
        properties: {
          addressPrefix: '10.40.0.0/22'
        }
      }
      {
        // The private endpoints (Azure SQL). The chart's network policy lets the API reach this range on 1433.
        name: 'private-endpoints'
        properties: {
          addressPrefix: '10.40.4.0/24'
        }
      }
    ]
  }
}

resource aksSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: vnet
  name: 'aks'
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: vnet
  name: 'private-endpoints'
}

// ---------------------------------------------------------------------------------------------------- Registry

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: names.acr
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    // Identities only: AKS pulls with its kubelet identity, GitHub Actions pushes with its own.
    adminUserEnabled: false
  }
}

// ---------------------------------------------------------------------------------------------------- AKS

// The cluster's control plane identity. It's made here, not by AKS, so that it has its role on the subnet before the
// cluster exists: with a system-assigned identity the role could only be given after the cluster was made, and AKS
// would start without the right to manage load balancer addresses in its subnet.
resource aksIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.aksIdentity
  location: location
}

// AKS manages load balancer addresses in its subnet.
resource aksNetworkContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: aksSubnet
  name: guid(aksSubnet.id, aksIdentity.id, roles.networkContributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.networkContributor)
    principalId: aksIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource aks 'Microsoft.ContainerService/managedClusters@2025-08-01' = {
  name: names.aks
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${aksIdentity.id}': {}
    }
  }
  dependsOn: [aksNetworkContributor]
  sku: {
    name: 'Base'
    tier: aksTier
  }
  properties: {
    dnsPrefix: names.aks
    enableRBAC: true
    // Entra ID sign-in only, with Azure RBAC for Kubernetes: no shared admin certificate.
    disableLocalAccounts: true
    aadProfile: {
      managed: true
      enableAzureRBAC: true
      adminGroupObjectIDs: [adminGroupObjectId]
    }
    // Workload identity: pods (External Secrets) sign in to Azure as a managed identity, with no stored secret.
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      // Cilium enforces the chart's NetworkPolicies.
      networkDataplane: 'cilium'
      networkPolicy: 'cilium'
      // Advanced Container Networking Services' security: Cilium's FQDN filtering, so the chart's CiliumNetworkPolicy
      // lets the API reach the SMTP relay, Key Vault and Microsoft Entra ID by name rather than every public address
      // (networkPolicy.fqdnEgress in values.yaml). Paid, per node (README.md, Cost). Observability (Hubble's metrics
      // and flow logs) stays off: filtering doesn't need it.
      advancedNetworking: {
        enabled: true
        observability: {
          enabled: false
        }
        security: {
          enabled: true
          advancedNetworkPolicies: 'FQDN'
        }
      }
      podCidr: '10.244.0.0/16'
      serviceCidr: '10.41.0.0/16'
      dnsServiceIP: '10.41.0.10'
      loadBalancerSku: 'standard'
    }
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        osType: 'Linux'
        osSKU: 'AzureLinux'
        vmSize: nodeVmSize
        type: 'VirtualMachineScaleSets'
        vnetSubnetID: aksSubnet.id
        enableAutoScaling: true
        count: nodeMinCount
        minCount: nodeMinCount
        maxCount: nodeMaxCount
      }
    ]
    autoUpgradeProfile: {
      upgradeChannel: 'patch'
      nodeOSUpgradeChannel: 'NodeImage'
    }
  }
}

// The nodes pull the API's image from the registry, as the kubelet identity AKS makes for them.
resource aksAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, aks.id, roles.acrPull)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

// MedApp's admins manage the cluster through Entra ID.
resource adminsClusterAdmin 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: aks
  name: guid(aks.id, adminGroupObjectId, roles.aksRbacClusterAdmin)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.aksRbacClusterAdmin)
    principalId: adminGroupObjectId
    principalType: 'Group'
  }
}

// ---------------------------------------------------------------------------------------------------- Azure SQL

resource sql 'Microsoft.Sql/servers@2023-08-01' = {
  name: names.sql
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Group'
      login: adminGroupName
      sid: adminGroupObjectId
      tenantId: tenant().tenantId
      // The app still signs in with the SQL login (see README.md, "Not yet").
      azureADOnlyAuthentication: false
    }
    minimalTlsVersion: '1.2'
    // Only through the private endpoint, from inside the virtual network.
    publicNetworkAccess: 'Disabled'
  }
}

// The server can't be deleted while this lock is on it, and neither can its database: deleting the server deletes
// the database's backups too. It also stops az group delete from deleting the resource group (README.md, Cost).
resource sqlLock 'Microsoft.Authorization/locks@2020-05-01' = {
  scope: sql
  name: 'medapp-keep-database'
  properties: {
    level: 'CanNotDelete'
    notes: 'MedApp\'s database and its backups. Remove this lock only to delete them on purpose (deploy/azure/README.md).'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sql
  name: 'MedApp'
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: sqlAutoPauseDelay
    minCapacity: json('0.5')
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

resource sqlPrivateDns 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  #disable-next-line no-hardcoded-env-urls
  name: 'privatelink.database.windows.net'
  location: 'global'
}

resource sqlPrivateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: sqlPrivateDns
  name: names.vnet
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-${names.sql}'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: names.sql
        properties: {
          privateLinkServiceId: sql.id
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource sqlPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: sqlPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql'
        properties: {
          privateDnsZoneId: sqlPrivateDns.id
        }
      }
    ]
  }
}

// ---------------------------------------------------------------------------------------------------- Key Vault

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: names.keyVault
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

// The chart's secrets, as External Secrets reads them: JSON, one property per key (deploy/azure/values.yaml).
resource databaseSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'medapp-db'
  properties: {
    contentType: 'application/json'
    value: string({
      'connection-string': 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Database=${database.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False'
    })
  }
}

resource firstAdminSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'medapp-first-admin'
  properties: {
    contentType: 'application/json'
    value: string({
      username: 'admin'
      password: firstAdminPassword
    })
  }
}

resource backplaneSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'medapp-backplane'
  properties: {
    contentType: 'application/json'
    value: string({
      password: backplanePassword
      'connection-string': 'medapp-valkey:6379,password=${backplanePassword}'
    })
  }
}

// The SMTP relay's user name and password (the Entra app's client secret). Made even before the app is set up, with
// empty values, so the API's Secret exists and it starts; until then, sending fails (and is logged).
resource emailSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'medapp-email'
  properties: {
    contentType: 'application/json'
    value: string({
      username: smtpConfigured ? names.smtpUsername : ''
      password: smtpPassword
    })
  }
}

// Wraps the API's data protection keys, which it keeps in the database: a copy of the database alone can't unseal
// reset and confirmation tokens. Made once: a later deployment leaves the key, and its versions, as they are. Only
// wrapping and unwrapping, and the private half never leaves Key Vault.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: 'medapp-data-protection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: ['wrapKey', 'unwrapKey']
  }
}

// ---------------------------------------------------------------------------------------------------- Email

// Azure Communication Services Email, with an Azure-managed sender domain (<random>.azurecomm.net): no DNS to set
// up, but low sending limits and a sender address nobody recognises (README.md, Email). Its data stays in the UK.
resource emailService 'Microsoft.Communication/emailServices@2025-09-01' = {
  name: names.emailService
  location: 'global'
  properties: {
    dataLocation: emailDataLocation
  }
}

resource emailDomain 'Microsoft.Communication/emailServices/domains@2025-09-01' = {
  parent: emailService
  // The one name Azure allows for its managed domain.
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
    // No tracking links rewritten into emails: they carry password reset links.
    userEngagementTracking: 'Disabled'
  }
}

resource communication 'Microsoft.Communication/communicationServices@2025-09-01' = {
  name: names.communication
  location: 'global'
  properties: {
    dataLocation: emailDataLocation
    linkedDomains: [emailDomain.id]
  }
}

// The SMTP relay signs in as the Entra app, which needs these rights on the Communication Services resource (a
// narrower role than the built-in Communication and Email Service Owner).
resource smtpSenderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = if (smtpConfigured) {
  name: guid(resourceGroup().id, 'medapp-smtp-sender')
  properties: {
    roleName: 'MedApp SMTP sender (${resourceGroup().name})'
    description: 'Sends email through Azure Communication Services\' SMTP relay (MedApp, deploy/azure).'
    type: 'CustomRole'
    assignableScopes: [resourceGroup().id]
    permissions: [
      {
        actions: [
          'Microsoft.Communication/CommunicationServices/Read'
          'Microsoft.Communication/CommunicationServices/Write'
          'Microsoft.Communication/EmailServices/write'
        ]
      }
    ]
  }
}

resource smtpSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (smtpConfigured) {
  scope: communication
  name: guid(communication.id, smtpAppPrincipalId, 'medapp-smtp-sender')
  properties: {
    roleDefinitionId: smtpSenderRole.id
    principalId: smtpAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// The user name the API signs in to smtp.azurecomm.net with; the password is one of the app's client secrets.
resource smtpUsername 'Microsoft.Communication/communicationServices/smtpUsernames@2025-09-01' = if (smtpConfigured) {
  parent: communication
  name: 'medapp'
  dependsOn: [smtpSender]
  properties: {
    username: names.smtpUsername
    entraApplicationId: smtpAppClientId
    tenantId: tenant().tenantId
  }
}

// ---------------------------------------------------------------------------------------------------- Identities

// External Secrets Operator reads Key Vault as this identity, through its Kubernetes service account.
resource externalSecretsIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.externalSecretsIdentity
  location: location
}

resource externalSecretsFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: externalSecretsIdentity
  name: 'external-secrets'
  properties: {
    issuer: aks.properties.oidcIssuerProfile.issuerURL
    subject: 'system:serviceaccount:external-secrets:external-secrets'
    audiences: ['api://AzureADTokenExchange']
  }
}

resource externalSecretsKeyVault 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, externalSecretsIdentity.id, roles.keyVaultSecretsUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: externalSecretsIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// The API wraps and unwraps its data protection keys with Key Vault's key as this identity, through its Kubernetes
// service account (the chart's api.workloadIdentity.clientId).
resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.apiIdentity
  location: location
}

resource apiFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: apiIdentity
  name: 'medapp-api'
  properties: {
    issuer: aks.properties.oidcIssuerProfile.issuerURL
    subject: apiServiceAccount
    audiences: ['api://AzureADTokenExchange']
  }
}

// On that one key only, not the vault: it can't read the vault's secrets or use any other key.
resource apiDataProtectionKey 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: dataProtectionKey
  name: guid(dataProtectionKey.id, apiIdentity.id, roles.keyVaultCryptoServiceEncryptionUser)
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      roles.keyVaultCryptoServiceEncryptionUser
    )
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// The Publish workflow pushes images as this identity, from the repository's main branch only (OIDC: no secret).
resource githubIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.githubIdentity
  location: location
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: githubIdentity
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:ref:refs/heads/main'
    audiences: ['api://AzureADTokenExchange']
  }
}

resource githubAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, githubIdentity.id, roles.acrPush)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPush)
    principalId: githubIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------------- Outputs

output aksName string = aks.name
output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output keyVaultUri string = keyVault.properties.vaultUri
output externalSecretsClientId string = externalSecretsIdentity.properties.clientId
// For the chart's values (deploy/azure/values.yaml): deploy.sh prints them.
output apiClientId string = apiIdentity.properties.clientId
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri
output emailSenderAddress string = 'DoNotReply@${emailDomain.properties.mailFromSenderDomain}'
output smtpConfigured bool = smtpConfigured
output githubClientId string = githubIdentity.properties.clientId
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
