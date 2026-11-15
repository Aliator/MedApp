// Parameters for main.bicep (deploy.sh passes this file). Fill in the Entra ID group, and set the three passwords
// as environment variables: they're never written to git. See README.md.
using 'main.bicep'

param environmentName = 'prod'

// The Entra ID group of MedApp's Azure admins: its object ID and display name.
param adminGroupObjectId = '00000000-0000-0000-0000-000000000000'
param adminGroupName = 'MedApp Admins'

param sqlAdminPassword = readEnvironmentVariable('MEDAPP_SQL_ADMIN_PASSWORD')
param firstAdminPassword = readEnvironmentVariable('MEDAPP_FIRST_ADMIN_PASSWORD')
param backplanePassword = readEnvironmentVariable('MEDAPP_BACKPLANE_PASSWORD')
