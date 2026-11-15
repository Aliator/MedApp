#!/usr/bin/env bash
# Sets up MedApp's AKS cluster once deploy.sh has made it: Traefik (ingress, with a public IP), cert-manager with a
# Let's Encrypt issuer, External Secrets Operator reading Key Vault with workload identity, and Argo CD following
# the env/azure branch. Safe to run again (it upgrades in place). See README.md.
#
#   deploy/azure/bootstrap.sh <resource group> <email for Let's Encrypt>
#
# Needs the Azure CLI (signed in, as a member of the admin group), kubectl, kubelogin and helm.
set -euo pipefail

group=${1:?usage: bootstrap.sh <resource group> <email for Let\'s Encrypt>}
email=${2:?usage: bootstrap.sh <resource group> <email for Let\'s Encrypt>}
deployment=medapp

cd "$(dirname "$0")/../.."  # the repo root
source deploy/versions.sh

for tool in az kubectl kubelogin helm; do
  command -v $tool >/dev/null || { echo "$tool isn't installed (deploy/azure/README.md)." >&2; exit 1; }
done

# The Azure CLI on Windows can end tsv output with a carriage return, which Git Bash's $(...) keeps: strip it.
outputs() {
  az deployment group show --resource-group "$group" --name "$deployment" --query "properties.outputs.$1.value" \
    --output tsv | tr -d '\r'
}
aks=$(outputs aksName)
key_vault_uri=$(outputs keyVaultUri)
external_secrets_client_id=$(outputs externalSecretsClientId)

echo "== Cluster credentials ($aks)"
# Entra ID sign-in (the cluster has no local accounts): kubelogin uses the Azure CLI's.
az aks get-credentials --resource-group "$group" --name "$aks" --overwrite-existing
kubelogin convert-kubeconfig --login azurecli
context=$aks
h() { helm --kube-context "$context" "$@"; }

echo "== Cluster services"
helm repo add traefik https://traefik.github.io/charts >/dev/null 2>&1 || true
helm repo add jetstack https://charts.jetstack.io >/dev/null 2>&1 || true
helm repo add external-secrets https://charts.external-secrets.io >/dev/null 2>&1 || true
helm repo add argo https://argoproj.github.io/argo-helm >/dev/null 2>&1 || true
helm repo update traefik jetstack external-secrets argo >/dev/null
h upgrade --install traefik traefik/traefik --version "$traefik_version" \
  --namespace traefik --create-namespace --values deploy/azure/traefik-values.yaml --wait
h upgrade --install cert-manager jetstack/cert-manager --version "$cert_manager_version" \
  --namespace cert-manager --create-namespace --set crds.enabled=true --wait
# External Secrets signs in to Key Vault as main.bicep's identity: its service account carries the identity's client
# ID, and its pods get the workload identity token.
h upgrade --install external-secrets external-secrets/external-secrets --version "$external_secrets_version" \
  --namespace external-secrets --create-namespace --wait \
  --set-string "serviceAccount.annotations.azure\.workload\.identity/client-id=$external_secrets_client_id" \
  --set-string "podLabels.azure\.workload\.identity/use=true"
h upgrade --install argocd argo/argo-cd --version "$argocd_version" \
  --namespace argocd --create-namespace --values deploy/azure/argocd-values.yaml --wait

echo "== Let's Encrypt and Key Vault"
# Escapes a value for the replacement side of sed's s|...|...|: a backslash, & (the matched text) and the delimiter.
sed_escape() { printf '%s' "$1" | sed -e 's/[\\&|]/\\&/g'; }
sed -e "s|\${LETSENCRYPT_EMAIL}|$(sed_escape "$email")|" -e "s|\${KEY_VAULT_URI}|$(sed_escape "$key_vault_uri")|" \
  deploy/azure/cluster.yaml | kubectl --context "$context" apply -f -

echo "== MedApp (Argo CD, from env/azure)"
# The project first: it limits what the Application may deploy (deploy/argocd/README.md).
kubectl --context "$context" apply -f deploy/argocd/project.yaml
kubectl --context "$context" apply -f deploy/argocd/medapp-azure.yaml

ip=$(kubectl --context "$context" --namespace traefik get service traefik -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo
echo "Done. Point your domain's DNS (ingress.host in deploy/azure/values.yaml) at $ip; cert-manager then gets its"
echo "certificate. Argo CD deploys MedApp once env/azure exists, after the first merge with the Azure repository"
echo "variables set (deploy.sh printed them). Its UI:"
echo "  kubectl --context $context -n argocd port-forward service/argocd-server 8080:443   # https://localhost:8080"
echo "  kubectl --context $context -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d"
