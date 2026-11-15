#!/usr/bin/env bash
# Runs MedApp on a local kind cluster with its Helm chart (deploy/helm/medapp): creates the cluster if it's missing,
# installs Traefik as the ingress controller, builds the API's image and loads it in, then installs or upgrades the
# chart, whose migrations Job brings the database up to date. Run it again after a code change to redeploy.
#
#   deploy/kind/up.sh                        # an empty database (no accounts: see README.md)
#   deploy/kind/up.sh --demo-data kind-demo  # replace the database with a medapp-dev snapshot (tools/MedApp.DevMcp/.backups)
#
# Needs Docker, kind, kubectl and helm. Only ever touches the kind-medapp context.
set -euo pipefail
# Git Bash on Windows would otherwise rewrite paths in the pod (/var/opt/...) as Windows paths.
export MSYS_NO_PATHCONV=1

cluster=medapp
context=kind-$cluster
release=medapp
traefik_version=42.3.2
demo=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --demo-data) demo=${2:?--demo-data needs a snapshot name or a .bak path}; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

cd "$(dirname "$0")/../.."  # the repo root

# winget puts kind and helm in a folder that's only on the PATH of shells started after they were installed.
if [[ -n ${LOCALAPPDATA:-} ]]; then
  PATH="$PATH:$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/Microsoft/WinGet/Links"
fi
for tool in docker kind kubectl helm; do
  command -v $tool >/dev/null || { echo "$tool isn't installed (see deploy/kind/README.md)." >&2; exit 1; }
done

k() { kubectl --context "$context" --namespace medapp "$@"; }
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
# Made by the first version of this script, before the Helm chart; Helm can't take them over.
if k get deployment medapp-api >/dev/null 2>&1 && [[ -z $(k get deployment medapp-api -o jsonpath='{.metadata.annotations.meta\.helm\.sh/release-name}') ]]; then
  echo "This cluster was set up before the Helm chart. Run deploy/kind/down.sh first, then this again." >&2
  exit 1
fi

echo "== Ingress (Traefik)"
helm repo add traefik https://traefik.github.io/charts >/dev/null 2>&1 || true
helm repo update traefik >/dev/null
h upgrade --install traefik traefik/traefik --version $traefik_version \
  --namespace traefik --create-namespace --values deploy/kind/traefik-values.yaml --wait

echo "== Image"
docker build -f MedApp.API/Dockerfile -t medapp-api:build .
# Tagged by the image's ID, so a changed image gets a new tag and Helm rolls it out; an unchanged one doesn't.
tag=local-$(docker image inspect medapp-api:build --format '{{.Id}}' | cut -d: -f2 | cut -c1-12)
docker tag medapp-api:build "medapp-api:$tag"
kind load docker-image "medapp-api:$tag" --name "$cluster"

chart=(upgrade --install "$release" deploy/helm/medapp --namespace medapp --create-namespace
  --values deploy/kind/values.yaml --set "image.tag=$tag" --wait --timeout 10m)

echo "== MedApp (Helm)"
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
echo "MedApp is running at http://localhost:8080"
