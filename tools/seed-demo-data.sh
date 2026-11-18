#!/usr/bin/env bash
# Loads the demo data (docs/demo-data.md) into the local development database. This script is the one way demo data
# gets in: never add demo or dummy records by hand, with SQL or through the app.
#
#   tools/seed-demo-data.sh --confirm --keep-users dev-parity            # the usual local run
#   tools/seed-demo-data.sh --confirm --out demo-summary.json            # the summary (with passwords) in a file
#   tools/seed-demo-data.sh --help                                       # every option
#
# What it does, in order:
#   1. builds the seeder (tools/MedApp.DevMcp) and the API (which the migrations tool starts), in Release, so a
#      running medapp-dev or AppHost isn't in the way
#   2. checks the database is a local one (the seeder refuses anything else)
#   3. applies any pending EF migrations (the app never migrates at startup)
#   4. runs the seeder: a pre-reset snapshot, then it WIPES the data and replays the demo centre's 18 months
#
# The database is the AppHost's SQL container (127.0.0.1,14330, database MedApp, the sa password from the AppHost's
# user secrets), so the AppHost must have run once and the container must be up. To load another local database
# (a throwaway one, say), set ConnectionStrings__DefaultConnection first, and pass --no-snapshot if its server has no
# backups folder. The summary is JSON on stdout; everything else is on stderr.
set -euo pipefail

cd "$(dirname "$0")/.."

confirm=false
for arg in "$@"; do
  case $arg in
    --confirm) confirm=true ;;
    -h | --help)
      dotnet run --project tools/MedApp.DevMcp -c Release -- seed-demo --help
      exit 0
      ;;
  esac
done

if ! $confirm; then
  echo "Loading the demo data WIPES the local database's data. Run it again with --confirm to go ahead." >&2
  exit 2
fi

conn=${ConnectionStrings__DefaultConnection:-}
if [[ -z $conn ]]; then
  password=$(dotnet user-secrets list --project MedApp.AppHost | tr -d '\r' | sed -n 's/^Parameters:sql-password = //p')
  if [[ -z $password ]]; then
    echo "No SQL password in the AppHost's user secrets. Run the AppHost once (dotnet run --project MedApp.AppHost), so Aspire creates it." >&2
    exit 1
  fi
  conn="Server=127.0.0.1,14330;Database=MedApp;User ID=sa;Password=$password;TrustServerCertificate=True"
fi
export ConnectionStrings__DefaultConnection=$conn

echo "Building the seeder..." >&2
dotnet build tools/MedApp.DevMcp -c Release --nologo -v quiet >&2
dotnet build MedApp.API -c Release --nologo -v quiet >&2

# Refuses a database that isn't local, before anything is changed.
dotnet run --no-build --project tools/MedApp.DevMcp -c Release -- seed-demo --check >&2

echo "Applying migrations..." >&2
dotnet ef database update --project MedApp.Infrastructure --startup-project MedApp.API --configuration Release \
  --no-build --connection "$conn" >&2

dotnet run --no-build --project tools/MedApp.DevMcp -c Release -- seed-demo "$@"
