# Loads the demo data (docs/demo-data.md) into the local development database. This script is the one way demo data
# gets in: never add demo or dummy records by hand, with SQL or through the app.
#
#   tools\seed-demo-data.ps1 --confirm --keep-users dev-parity            # the usual local run
#   tools\seed-demo-data.ps1 --confirm --out demo-summary.json            # the summary (with passwords) in a file
#   tools\seed-demo-data.ps1 --help                                       # every option
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
# (a throwaway one, say), set $env:ConnectionStrings__DefaultConnection first, and pass --no-snapshot if its server
# has no backups folder. The summary is JSON on the output stream; everything else goes to the host.
#
# The same as tools/seed-demo-data.sh, for Windows PowerShell 5.1 and later. Options are passed as they are, so they
# keep their two dashes (--confirm).

# 'Continue', not 'Stop': Windows PowerShell 5.1 turns a native command's stderr into an error record whenever the
# caller redirects it (*>&1, | Tee-Object), and 'Stop' would end the script at the seeder's first progress line. Every
# native step checks its exit code instead (Assert-Succeeded).
$ErrorActionPreference = 'Continue'
$seedArgs = $args

function Assert-Succeeded([string]$step) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$step failed (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

Push-Location (Join-Path $PSScriptRoot '..')
try {
    if ($seedArgs -contains '--help' -or $seedArgs -contains '-h') {
        dotnet run --project tools/MedApp.DevMcp -c Release -- seed-demo --help
        exit $LASTEXITCODE
    }

    if ($seedArgs -notcontains '--confirm') {
        Write-Host "Loading the demo data WIPES the local database's data. Run it again with --confirm to go ahead."
        exit 2
    }

    $conn = $env:ConnectionStrings__DefaultConnection
    if ([string]::IsNullOrWhiteSpace($conn)) {
        $prefix = 'Parameters:sql-password = '
        $line = dotnet user-secrets list --project MedApp.AppHost | Where-Object { $_.StartsWith($prefix) } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($line)) {
            Write-Host "No SQL password in the AppHost's user secrets. Run the AppHost once (dotnet run --project MedApp.AppHost), so Aspire creates it."
            exit 1
        }

        $password = $line.Substring($prefix.Length).Trim()
        $conn = "Server=127.0.0.1,14330;Database=MedApp;User ID=sa;Password=$password;TrustServerCertificate=True"
    }

    $previous = $env:ConnectionStrings__DefaultConnection
    $env:ConnectionStrings__DefaultConnection = $conn
    try {
        Write-Host 'Building the seeder...'
        dotnet build tools/MedApp.DevMcp -c Release --nologo -v quiet | Out-Host
        Assert-Succeeded 'The build'
        dotnet build MedApp.API -c Release --nologo -v quiet | Out-Host
        Assert-Succeeded 'The build of the API'

        # Refuses a database that isn't local, before anything is changed.
        dotnet run --no-build --project tools/MedApp.DevMcp -c Release -- seed-demo --check | Out-Host
        Assert-Succeeded 'The check of the database'

        Write-Host 'Applying migrations...'
        dotnet ef database update --project MedApp.Infrastructure --startup-project MedApp.API --configuration Release --no-build --connection $conn | Out-Host
        Assert-Succeeded 'Applying migrations'

        dotnet run --no-build --project tools/MedApp.DevMcp -c Release -- seed-demo @seedArgs
        exit $LASTEXITCODE
    }
    finally {
        $env:ConnectionStrings__DefaultConnection = $previous
    }
}
finally {
    Pop-Location
}
