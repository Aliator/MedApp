#!/usr/bin/env bash
# The tier chooser's cases (ci-plan.sh), as a table. The `changes` job runs it before choosing, so a broken rule fails
# there (and everything then runs). Locally: bash .github/scripts/ci-plan.test.sh
set -uo pipefail
cd "$(dirname "$0")"
failed=0

# check <name> <labels> <files, separated by spaces> <expected: tier web web_build dotnet integration image e2e helm coverage>
# EVENT and KNOWN come from the environment (a pull request whose files are all known, unless a case says otherwise).
check() {
  local name=$1 labels=$2 files=$3 expected=$4 plan got
  plan=$(LABELS="$labels" EVENT="${EVENT:-pull_request}" DISPATCH_TIER="${DISPATCH_TIER:-}" KNOWN="${KNOWN:-true}" ./ci-plan.sh <<< "${files// /$'\n'}") || { echo "FAIL $name: ci-plan.sh failed"; failed=1; return; }
  got=$(sed -n -E 's/^(tier|web|web_build|dotnet|integration|image|e2e|helm|coverage)=//p' <<< "$plan" | tr '\n' ' ')
  if [ "${got% }" = "$expected" ]; then
    echo "ok   $name"
  else
    echo "FAIL $name"; echo "       expected: $expected"; echo "       got:      ${got% }"; echo "       $(grep '^reason=' <<< "$plan")"
    failed=1
  fi
}

page=MedApp.Web/src/pages/patients/PatientsPage.tsx
handler=MedApp.Application/Patients/Commands/UpdatePatient/UpdatePatientHandler.cs
migration=MedApp.Infrastructure/Migrations/20261101000000_AddedSomething.cs

#                                                                tier  web   build dotnet integ image e2e   helm  coverage
# No label: the default, by what changed.
check 'web only'              '' "$page"                        'light light false false false false false false false'
check '.NET only'             '' "$handler"                     'light none false true false false false false false'
check 'both sides'            '' "$page $handler"               'light light false true false false false false false'
check 'a test project only'   '' MedApp.UnitTests/Some/Tests.cs 'light none false true false false false false false'
check 'a migration'           '' "$migration"                   'light none false true false false false false false'
check 'the browser demo backend' '' MedApp.Browser/Hosting/ApiRoutes.cs 'light none false true false false false false false'
check 'web build input'       '' MedApp.Web/server/server.mjs   'light light true false false false false false false'
check 'web entry file'        '' MedApp.Web/src/entry-server.tsx 'light light true false false false false false false'
check 'other labels'          'dependencies javascript' "$page" 'light light false false false false false false false'

# Docs only: nothing, whatever the label.
check 'docs only'             '' 'docs/todo.md docs/reviews/pr-1.md' 'none none false false false false false false false'
check 'root Markdown only'    '' 'AGENTS.md CLAUDE.md'          'none none false false false false false false false'
check 'screenshot tool only'  '' tools/ui-parity/screens.ts     'none none false false false false false false false'
check 'docs only, ci:full'    'ci:full' docs/todo.md            'none none false false false false false false false'
check 'no files at all'       '' ''                             'none none false false false false false false false'
check 'docs with code'        '' "docs/todo.md $handler"        'light none false true false false false false false'

# ci:none: a small fix skips everything...
check 'ci:none, web'          'ci:none' "$page"                 'none none false false false false false false false'
check 'ci:none, .NET'         'ci:none' "$handler"              'none none false false false false false false false'
check 'ci:none, a migration'  'ci:none' "$migration"            'none none false false false false false false false'
check 'ci:none among others'  'bug ci:none' "$page $handler"    'none none false false false false false false false'
# ...but never a workflow, deploy/, the Dockerfile or a package manifest or lock.
check 'ci:none, workflow'     'ci:none' ".github/workflows/main.yml $page" 'light light false true false false false false false'
check 'ci:none, deploy'       'ci:none' "deploy/helm/medapp/values.yaml $handler" 'light none false true false false false true false'
check 'ci:none, Dockerfile'   'ci:none' MedApp.API/Dockerfile   'light none false false false true false false false'
check 'ci:none, .dockerignore' 'ci:none' .dockerignore          'light none false false false true false false false'
check 'ci:none, npm lock'     'ci:none' MedApp.Web/package-lock.json 'light light true false false true false false false'
check 'ci:none, image csproj' 'ci:none' MedApp.API/MedApp.API.csproj 'light none false true false true false false false'

# Never light, without a label.
check 'workflow'              '' .github/workflows/main.yml     'light light false true false false false false false'
check 'CI script'             '' .github/scripts/ci-plan.sh     'light light false true false false false false false'
check 'deploy only'           '' deploy/kind/values.yaml        'light none false false false false false true false'
check 'Dockerfile'            '' MedApp.API/Dockerfile          'light none false false false true false false false'
check 'package.json'          '' MedApp.Web/package.json        'light light true false false true false false false'
check 'global.json'           '' global.json                    'light none false true false true false false false'
check 'test project csproj'   '' MedApp.UnitTests/MedApp.UnitTests.csproj 'light none false true false false false false false'
check '.trivyignore'          '' .trivyignore                   'light none false false false true false false false'

# ci:full: everything for the sides that changed.
check 'ci:full, web'          'ci:full' "$page"                 'full full true false false true true false true'
check 'ci:full, .NET'         'ci:full' "$handler"              'full none false true true true true false true'
check 'ci:full, both'         'ci:full' "$page $handler"        'full full true true true true true false true'
check 'ci:full, deploy only'  'ci:full' deploy/kind/values.yaml 'full none false false false false false true true'
check 'both labels'           'ci:none ci:full' "$page"         'full full true false false true true false true'
check 'both labels, reversed' 'ci:full ci:none' "$handler"      'full none false true true true true false true'

# A file moved across sides counts by both names (the caller lists both).
check 'moved web to .NET'     '' "MedApp.Web/src/x.ts MedApp.API/x.ts" 'light light false true false false false false false'
check 'moved out of docs'     '' "docs/x.md MedApp.API/x.md"    'light none false true false false false false false'

# A file no rule knows: both sides get their checks.
check 'unknown root file'     '' .gitignore                     'light light false true false false false false false'
check 'unknown folder'        '' tools/new-tool/run.sh          'light light false true false false false false false'
check 'unknown, ci:none'      'ci:none' .mcp.json               'none none false false false false false false false'
check 'nested Markdown'       '' MedApp.Web/README.md           'light light false false false false false false false'

# Not known, or not a pull request: everything.
KNOWN=false check '300 files or more'   '' "$page"              'full full true true true true true true true'
KNOWN=false check 'not known, ci:none'  'ci:none' docs/todo.md  'full full true true true true true true true'
EVENT=push check 'a push to main'       '' "$page"              'light light false true false false false false false'
EVENT=workflow_dispatch DISPATCH_TIER=phase check 'a phase run' '' '' 'phase full true true false false false false false'
EVENT=workflow_dispatch DISPATCH_TIER=full check 'a full run'   '' '' 'full full true true true true true true false'

[ "$failed" = 0 ] && echo "All cases passed." || { echo "Some cases failed."; exit 1; }
