# The cluster services' Helm chart versions, shared by deploy/kind/up.sh and deploy/azure/bootstrap.sh, so the local
# cluster runs what Azure does. Upgrade them here, together, after reading their release notes.
traefik_version=42.3.2
cert_manager_version=v1.21.2
external_secrets_version=2.14.0
argocd_version=10.17.2
