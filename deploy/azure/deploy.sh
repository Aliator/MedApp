#!/usr/bin/env bash
# Creates or updates MedApp's Azure resources (main.bicep) in a resource group: shows what would change (what-if),
# asks, then deploys. Then run bootstrap.sh to set up the cluster. See README.md.
#
#   deploy/azure/deploy.sh <resource group> [location]
#
# Needs the Azure CLI, signed in (az login) to the subscription to use, and the three passwords as environment
# variables (MEDAPP_SQL_ADMIN_PASSWORD, MEDAPP_FIRST_ADMIN_PASSWORD, MEDAPP_BACKPLANE_PASSWORD): set the same ones on
# every run, or Azure SQL's login and Key Vault's secrets change to the new ones. Once main.bicepparam has the email
# app, MEDAPP_SMTP_PASSWORD too (its client secret). This costs money from the moment it runs: see README.md.
set -euo pipefail

group=${1:?usage: deploy.sh <resource group> [location]}
location=${2:-uksouth}
deployment=medapp

cd "$(dirname "$0")"

command -v az >/dev/null || { echo "The Azure CLI isn't installed (README.md)." >&2; exit 1; }
for name in MEDAPP_SQL_ADMIN_PASSWORD MEDAPP_FIRST_ADMIN_PASSWORD MEDAPP_BACKPLANE_PASSWORD; do
  [[ -n ${!name:-} ]] || { echo "$name isn't set (README.md, Passwords)." >&2; exit 1; }
done
# Email's Entra app (main.bicepparam): once it's set, its client secret must be too, or this run would empty the SMTP
# password in Key Vault.
smtp_app_set=true
grep -q "^param smtpAppClientId = ''" main.bicepparam && smtp_app_set=false
if $smtp_app_set && [[ -z ${MEDAPP_SMTP_PASSWORD:-} ]]; then
  echo "MEDAPP_SMTP_PASSWORD isn't set, but main.bicepparam has the email app (smtpAppClientId): set it to the app's" >&2
  echo "client secret (README.md, Email)." >&2
  exit 1
fi
# The placeholder admin group would only fail after AKS is made (about ten minutes in), on the group's role.
if grep -q '00000000-0000-0000-0000-000000000000' main.bicepparam; then
  echo "main.bicepparam still has the placeholder adminGroupObjectId (all zeros). Set it to your admin group's" >&2
  echo "object ID first (README.md, step 1)." >&2
  exit 1
fi

# The Azure CLI on Windows can end tsv output with a carriage return, which Git Bash's $(...) keeps: strip it.
echo "Subscription: $(az account show --query '[name, id]' --output tsv | tr -d '\r' | paste -sd ' ')"
az group create --name "$group" --location "$location" --output none

echo "== What would change"
az deployment group what-if --resource-group "$group" --name "$deployment" --parameters main.bicepparam

read -r -p "Deploy these changes to $group? [y/N] " answer
[[ $answer == [yY] ]] || { echo "Nothing deployed."; exit 0; }

echo "== Deploying"
az deployment group create --resource-group "$group" --name "$deployment" --parameters main.bicepparam --output none

outputs() {
  az deployment group show --resource-group "$group" --name "$deployment" --query "properties.outputs.$1.value" \
    --output tsv | tr -d '\r'
}
echo
echo "Deployed. Next:"
echo "  1. deploy/azure/bootstrap.sh $group <email for Let's Encrypt>"
echo "  2. In GitHub (Settings, Secrets and variables, Actions, Variables), set these repository variables, so the"
echo "     Publish workflow deploys each merge to Azure:"
echo "       AZURE_CLIENT_ID=$(outputs githubClientId)"
echo "       AZURE_TENANT_ID=$(outputs tenantId)"
echo "       AZURE_SUBSCRIPTION_ID=$(outputs subscriptionId)"
echo "       AZURE_ACR_NAME=$(outputs acrName)"
echo "  3. In deploy/azure/values.yaml, set these, and merge that to main before MedApp's first deploy:"
echo "       api.workloadIdentity.clientId: $(outputs apiClientId)"
echo "       email.fromAddress: $(outputs emailSenderAddress)"
echo "       dataProtection.keyVaultKeyUri: $(outputs dataProtectionKeyUri)"
smtp_configured=$(outputs smtpConfigured)
if [[ ${smtp_configured,,} != true ]]; then  # tsv prints a bool as True or true, depending on the CLI
  echo
  echo "Email can't be sent yet: there's no Entra app for the SMTP relay in main.bicepparam (README.md, Email)."
fi
