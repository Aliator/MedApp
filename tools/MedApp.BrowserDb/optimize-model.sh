#!/usr/bin/env bash
# Makes the browser's compiled model (MedApp.Browser/Data/CompiledModel) from the model as the app builds it, fitted to
# SQLite (SqliteModelCustomizer). Run it after any change to the entities or their EF configurations: the demo
# database's build (tools/MedApp.BrowserDb) refuses a compiled model that no longer matches. Needs dotnet-ef 10.x.
set -euo pipefail

cd "$(dirname "$0")/../.."

rm -rf MedApp.Browser/Data/CompiledModel
dotnet ef dbcontext optimize \
  --project tools/MedApp.BrowserDb --startup-project tools/MedApp.BrowserDb --configuration Release \
  --context MedAppDbContext \
  --output-dir ../../MedApp.Browser/Data/CompiledModel --namespace MedApp.Browser.Data.CompiledModel
