#!/usr/bin/env bash
# Runs MedApp on a local kind cluster: creates the cluster if it's missing, builds the API's image and loads it in,
# deploys SQL Server and the API, and applies the EF migrations. Run it again after a code change to redeploy.
#
#   deploy/kind/up.sh                        # an empty database (no accounts: see README.md)
#   deploy/kind/up.sh --demo-data kind-demo  # first restore a medapp-dev snapshot (tools/MedApp.DevMcp/.backups)
#
# Needs Docker, kind, kubectl and the dotnet-ef tool. Only ever touches the kind-medapp context.
set -euo pipefail
# Git Bash on Windows would otherwise rewrite paths in the pod (/var/opt/...) as Windows paths.
export MSYS_NO_PATHCONV=1

cluster=medapp
context=kind-$cluster
image=medapp-api:local
demo=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --demo-data) demo=${2:?--demo-data needs a snapshot name or a .bak path}; shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

cd "$(dirname "$0")/../.."  # the repo root

# winget puts kind in a folder that's only on the PATH of shells started after it was installed.
if ! command -v kind >/dev/null && [[ -n ${LOCALAPPDATA:-} ]]; then
  PATH="$PATH:$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/Microsoft/WinGet/Links"
fi
for tool in docker kind kubectl dotnet; do
  command -v $tool >/dev/null || { echo "$tool isn't installed (see deploy/kind/README.md)." >&2; exit 1; }
done

k() { kubectl --context "$context" --namespace medapp "$@"; }
# sqlcmd in the SQL Server pod, signed in as sa with the pod's own password (it never leaves the cluster).
sql() { k exec -i sql-0 -- sh -c '/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" "$@"' sqlcmd "$@"; }

if [[ -n $demo ]]; then
  bak=$demo
  [[ $bak == *.bak ]] || bak=tools/MedApp.DevMcp/.backups/$demo.bak
  [[ -f $bak ]] || { echo "No snapshot at $bak. Save one with medapp-dev's save_snapshot." >&2; exit 1; }
fi

echo "== Cluster"
kind get clusters 2>/dev/null | grep -qx "$cluster" || kind create cluster --config deploy/kind/cluster.yaml

echo "== Image"
docker build -f MedApp.API/Dockerfile -t "$image" .
kind load docker-image "$image" --name "$cluster"

echo "== Secret"
kubectl --context "$context" apply -f deploy/kind/namespace.yaml
if ! k get secret medapp-sql >/dev/null 2>&1; then
  # Letters and digits only, so it needs no escaping in the connection string; the A and 1 meet SQL Server's rules.
  password="A1$(openssl rand -hex 16)"
  k create secret generic medapp-sql \
    --from-literal=sa-password="$password" \
    --from-literal=connection-string="Server=sql,1433;Database=MedApp;User ID=sa;Password=$password;TrustServerCertificate=True"
fi

echo "== Deploy"
kubectl --context "$context" apply -k deploy/kind
k rollout status statefulset/sql --timeout=5m
until sql -Q "SELECT 1" >/dev/null 2>&1; do sleep 3; done

if [[ -n $demo ]]; then
  echo "== Demo data ($bak)"
  # The API holds connections to the database, which RESTORE needs to itself.
  k scale deployment/medapp-api --replicas=0
  k wait --for=delete pod -l app=medapp-api --timeout=2m 2>/dev/null || true  # errors when there are none
  k exec -i sql-0 -- sh -c 'cat > /var/opt/mssql/restore.bak' < "$bak"
  sql -Q "RESTORE DATABASE [MedApp] FROM DISK = N'/var/opt/mssql/restore.bak' WITH REPLACE"
  k exec sql-0 -- rm /var/opt/mssql/restore.bak
fi

echo "== Migrations"
password=$(k get secret medapp-sql -o jsonpath='{.data.sa-password}' | base64 -d)
# Release builds to its own folder, so this works while the AppHost runs the API (and locks its Debug build).
dotnet ef database update --project MedApp.Infrastructure --startup-project MedApp.API --configuration Release \
  --connection "Server=127.0.0.1,14331;Database=MedApp;User ID=sa;Password=$password;TrustServerCertificate=True"

echo "== API"
k scale deployment/medapp-api --replicas=1
# The tag doesn't change between builds, so restart to pick up the image just loaded.
k rollout restart deployment/medapp-api
k rollout status deployment/medapp-api --timeout=3m

echo
echo "MedApp is running at http://localhost:8080"
