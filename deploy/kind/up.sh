#!/usr/bin/env bash
# Runs MedApp on a local kind cluster with its Helm chart (deploy/helm/medapp): creates the cluster if it's missing,
# installs what a real cluster would have (Traefik as the ingress controller, cert-manager for TLS, External
# Secrets Operator with a local stand-in for a secret store), builds the API's image and loads it in, then installs
# or upgrades the chart, whose migrations Job brings the database up to date and makes the first admin.
# Run it again after a code change to redeploy.
#
#   deploy/kind/up.sh                        # an empty database, with a first admin (see README.md)
#   deploy/kind/up.sh --demo-data kind-demo  # replace the database with a medapp-dev snapshot (tools/MedApp.DevMcp/.backups)
#   deploy/kind/up.sh --argocd               # GitOps instead: Argo CD deploys what's in git (deploy/argocd/README.md)
#   deploy/kind/up.sh --argocd --revision feature/x   # ... with the chart and values from a branch
#
# Needs Docker, kind, kubectl, helm and openssl. Only ever touches the kind-medapp context.
set -euo pipefail
# Git Bash on Windows would otherwise rewrite paths in the pod (/var/opt/...) as Windows paths.
export MSYS_NO_PATHCONV=1

cluster=medapp
context=kind-$cluster
release=medapp
traefik_version=42.3.2
cert_manager_version=v1.21.2
external_secrets_version=2.14.0
argocd_version=10.17.2
demo=""
argocd=false
revision=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --demo-data) demo=${2:?--demo-data needs a snapshot name or a .bak path}; shift 2 ;;
    --argocd) argocd=true; shift ;;
    --revision) revision=${2:?--revision needs a branch, tag or commit}; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
if $argocd && [[ -n $demo ]]; then
  # Argo CD puts back anything changed by hand, such as the API scaled to none for a restore.
  echo "--demo-data works only without --argocd." >&2
  exit 2
fi
if [[ -n $revision ]] && ! $argocd; then
  echo "--revision works only with --argocd." >&2
  exit 2
fi

cd "$(dirname "$0")/../.."  # the repo root

# winget puts kind and helm in a folder that's only on the PATH of shells started after they were installed.
if [[ -n ${LOCALAPPDATA:-} ]]; then
  PATH="$PATH:$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/Microsoft/WinGet/Links"
fi
for tool in docker kind kubectl helm openssl; do
  command -v $tool >/dev/null || { echo "$tool isn't installed (see deploy/kind/README.md)." >&2; exit 1; }
done

k() { kubectl --context "$context" --namespace medapp "$@"; }
vault() { kubectl --context "$context" --namespace medapp-vault "$@"; }
h() { helm --kube-context "$context" "$@"; }
# sqlcmd in the SQL Server pod, signed in as sa with the pod's own password (it never leaves the cluster).
sql() { k exec -i $release-sql-0 -- sh -c '/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" "$@"' sqlcmd "$@"; }

if [[ -n $demo ]]; then
  bak=$demo
  [[ $bak == *.bak ]] || bak=tools/MedApp.DevMcp/.backups/$demo.bak
  [[ -f $bak ]] || { echo "No snapshot at $bak. Save one with medapp-dev's save_snapshot." >&2; exit 1; }
fi

echo "== Cluster"
kind get clusters 2>/dev/null | grep -qx "$cluster" || kind create cluster --config deploy/kind/cluster.yaml
# Made before the https port (8443) was added to cluster.yaml; kind can't add it to a cluster that exists.
if ! docker port $cluster-control-plane 30443 >/dev/null 2>&1; then
  echo "This cluster was made before https://localhost:8443 was added. Run deploy/kind/down.sh first, then this again." >&2
  exit 1
fi

echo "== Cluster services (Traefik, cert-manager, External Secrets)"
helm repo add traefik https://traefik.github.io/charts >/dev/null 2>&1 || true
helm repo add jetstack https://charts.jetstack.io >/dev/null 2>&1 || true
helm repo add external-secrets https://charts.external-secrets.io >/dev/null 2>&1 || true
helm repo update traefik jetstack external-secrets >/dev/null
h upgrade --install traefik traefik/traefik --version $traefik_version \
  --namespace traefik --create-namespace --values deploy/kind/traefik-values.yaml --wait
h upgrade --install cert-manager jetstack/cert-manager --version $cert_manager_version \
  --namespace cert-manager --create-namespace --set crds.enabled=true --wait
h upgrade --install external-secrets external-secrets/external-secrets --version $external_secrets_version \
  --namespace external-secrets --create-namespace --wait
kubectl --context "$context" apply -f deploy/kind/local-cluster.yaml

echo "== Secret store"
# The local vault's secrets, made once with random passwords (none is in git). The chart reads them through
# External Secrets; they last as long as the cluster.
if ! vault get secret medapp-db >/dev/null 2>&1; then
  # Letters and digits only, so it needs no escaping in the connection string; A, a and 1 meet SQL Server's rules.
  password="Aa1$(openssl rand -hex 16)"
  vault create secret generic medapp-db \
    --from-literal=sa-password="$password" \
    --from-literal=connection-string="Server=$release-sql,1433;Database=MedApp;User ID=sa;Password=$password;TrustServerCertificate=True"
fi
if ! vault get secret medapp-first-admin >/dev/null 2>&1; then
  vault create secret generic medapp-first-admin \
    --from-literal=username=admin --from-literal=password="Aa1$(openssl rand -hex 12)"
fi

# Who deploys MedApp: Helm from this script, or Argo CD from git. Not both at once.
helm_release=$(h list --namespace medapp --filter "^$release\$" --short 2>/dev/null || true)
argocd_app=$(kubectl --context "$context" --namespace argocd get application medapp --ignore-not-found -o name 2>/dev/null || true)

if $argocd; then
  if [[ -n $helm_release ]]; then
    echo "MedApp was deployed by up.sh without --argocd. Remove that first (the database is kept):" >&2
    echo "  helm --kube-context $context --namespace medapp uninstall $release" >&2
    exit 1
  fi

  echo "== Argo CD"
  helm repo add argo https://argoproj.github.io/argo-helm >/dev/null 2>&1 || true
  helm repo update argo >/dev/null
  h upgrade --install argocd argo/argo-cd --version $argocd_version \
    --namespace argocd --create-namespace --values deploy/argocd/argocd-values.yaml --wait

  echo "== MedApp (Argo CD)"
  kubectl --context "$context" apply -f deploy/argocd/medapp-kind.yaml
  if [[ -n $revision ]]; then
    # The chart and the cluster's values from that revision; the image still from env/kind.
    kubectl --context "$context" --namespace argocd patch application medapp --type json -p "[
      {\"op\": \"replace\", \"path\": \"/spec/sources/0/targetRevision\", \"value\": \"$revision\"},
      {\"op\": \"replace\", \"path\": \"/spec/sources/1/targetRevision\", \"value\": \"$revision\"}]"
  fi
  echo "Waiting for Argo CD to sync it from git..."
  kubectl --context "$context" --namespace argocd wait application/medapp --timeout=15m \
    --for=jsonpath='{.status.sync.status}'=Synced
  kubectl --context "$context" --namespace argocd wait application/medapp --timeout=15m \
    --for=jsonpath='{.status.health.status}'=Healthy

  echo
  echo "MedApp is running at http://localhost:8080 and https://localhost:8443, deployed by Argo CD."
  echo "Argo CD is at http://argocd.localhost:8080: sign in as admin, with the password from:"
  echo "  kubectl --context $context -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d"
  echo "MedApp's first admin is 'admin', with the password from:"
  echo "  kubectl --context $context -n medapp-vault get secret medapp-first-admin -o jsonpath='{.data.password}' | base64 -d"
  exit 0
fi

if [[ -n $argocd_app ]]; then
  echo "MedApp is deployed by Argo CD (up.sh --argocd). Remove that first (the database is kept):" >&2
  echo "  kubectl --context $context --namespace argocd delete application medapp" >&2
  exit 1
fi

echo "== Image"
docker build -f MedApp.API/Dockerfile -t medapp-api:build .
# Tagged by the image's ID, so a changed image gets a new tag and Helm rolls it out; an unchanged one doesn't.
tag=local-$(docker image inspect medapp-api:build --format '{{.Id}}' | cut -d: -f2 | cut -c1-12)
docker tag medapp-api:build "medapp-api:$tag"
kind load docker-image "medapp-api:$tag" --name "$cluster"

echo "== MedApp (Helm)"
# The namespace refuses any pod that doesn't meet the "restricted" Pod Security Standard.
kubectl --context "$context" create namespace medapp --dry-run=client -o yaml | kubectl --context "$context" apply -f -
kubectl --context "$context" label namespace medapp --overwrite \
  pod-security.kubernetes.io/enforce=restricted pod-security.kubernetes.io/warn=restricted
chart=(upgrade --install "$release" deploy/helm/medapp --namespace medapp
  --values deploy/kind/values.yaml --set "image.tag=$tag" --wait --timeout 10m)
h "${chart[@]}"

if [[ -n $demo ]]; then
  echo "== Demo data ($bak)"
  # The API holds connections to the database, which RESTORE needs to itself.
  h "${chart[@]}" --set api.replicas=0
  k wait --for=delete pod -l app.kubernetes.io/component=api --timeout=2m 2>/dev/null || true  # errors when there are none
  k exec -i $release-sql-0 -- sh -c 'cat > /var/opt/mssql/restore.bak' < "$bak"
  sql -Q "RESTORE DATABASE [MedApp] FROM DISK = N'/var/opt/mssql/restore.bak' WITH REPLACE"
  k exec $release-sql-0 -- rm /var/opt/mssql/restore.bak
  # Back to one copy; the upgrade's migrations Job brings the restored database up to date first.
  h "${chart[@]}"
fi

echo
echo "MedApp is running at http://localhost:8080 and https://localhost:8443 (a self-signed certificate)."
if [[ -n $demo ]]; then
  echo "Sign in with the demo data's accounts (admin / admin)."
else
  echo "The first admin is 'admin', with the password from:"
  echo "  kubectl --context $context -n medapp-vault get secret medapp-first-admin -o jsonpath='{.data.password}' | base64 -d"
fi
