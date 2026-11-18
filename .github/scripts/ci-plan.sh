#!/usr/bin/env bash
# Chooses what CI runs for a pull request (the `changes` job of .github/workflows/main.yml): a tier, and from it which
# jobs run. It only reads its input, so ci-plan.test.sh can test it as a table of cases.
#
#   ci-plan.sh < changed files, one name a line (a renamed file by both its names)
#
#   EVENT   the workflow's event: pull_request, or anything else (a push to main, the nightly run), which is always full
#   LABELS  the pull request's labels, separated by spaces
#   KNOWN   false when the list of files isn't all of them (the comparison failed, or has 300 files or more)
#
# It prints name=value lines: tier (none, light or full), reason, and the jobs: web (none, light or full), web_build,
# dotnet, integration, image, e2e, helm and coverage (true or false).
#
# The tiers (AGENTS.md, CI):
#   none   nothing builds or tests: only docs changed, or the `ci:none` label (a small fix)
#   light  the default: the side that changed gets its quick checks (web: lint, type check and the changed files' own
#          Vitest tests; .NET: the solution's build and the unit tests). No integration tests, E2E or image.
#   full   everything: the `ci:full` label, and always on main. Still by side: a web-only change runs no .NET tests.
#
# Never light, whatever the label: a workflow change (.github/) runs everything; deploy/ runs helm; the Dockerfile,
# .dockerignore and the package manifests and locks run image (with its Trivy scan). `ci:none` isn't honoured for them.
set -euo pipefail

event=${EVENT:-pull_request}
labels=" ${LABELS:-} "
known=${KNOWN:-true}

# ---- The path rules: every changed file is one of these, by the first that matches. ----
web=false         # MedApp.Web/: the React app
web_build=false   # ...and what its production build reads beyond the source the tests cover
dotnet=false      # the .NET projects, the solution and the SDK version
image=false       # what decides the image's packages and how it's built
deploy=false      # deploy/: the chart, the cluster services' values, the Bicep
workflow=''       # .github/: the first such file
unknown=''        # the first file no rule knows: both sides get their checks

while IFS= read -r file; do
  case "$file" in
    '') ;;
    # Nothing in CI reads these: docs, and the screenshot tool (a development aid, run by hand).
    docs/* | tools/ui-parity/*) ;;
    .github/*) workflow=${workflow:-$file} ;;
    deploy/*) deploy=true ;;
    MedApp.API/Dockerfile | .dockerignore | .trivyignore) image=true ;;
    MedApp.Web/package.json | MedApp.Web/package-lock.json) web=true; web_build=true; image=true ;;
    MedApp.Web/vite.config.ts | MedApp.Web/tsconfig*.json | MedApp.Web/index.html | MedApp.Web/server/* | MedApp.Web/src/entry-*.tsx)
      web=true; web_build=true ;;
    MedApp.Web/*) web=true ;;
    # The projects the Dockerfile restores: their packages are the image's.
    MedApp.API/*.csproj | MedApp.Application/*.csproj | MedApp.Contracts/*.csproj | MedApp.Domain/*.csproj | MedApp.Infrastructure/*.csproj | MedApp.ServiceDefaults/*.csproj | global.json)
      dotnet=true; image=true ;;
    MedApp.API/* | MedApp.Application/* | MedApp.Contracts/* | MedApp.Domain/* | MedApp.Infrastructure/* | MedApp.ServiceDefaults/* | \
    MedApp.UnitTests/* | MedApp.IntegrationTests/* | MedApp.E2ETests/* | MedApp.AppHost/* | MedApp.Client.Deprecated/* | tools/MedApp.DevMcp/* | MedApp.sln)
      dotnet=true ;;
    # A Markdown file at the root (AGENTS.md, CLAUDE.md): nothing reads it.
    */*) unknown=${unknown:-$file} ;;
    *.md) ;;
    *) unknown=${unknown:-$file} ;;
  esac
done

if [ -n "$unknown" ]; then web=true; dotnet=true; fi
has() { [[ $labels == *" $1 "* ]]; }

# ---- The tier. ----
everything=false
if [ "$event" = workflow_dispatch ]; then
  # Run by hand: `phase` at the end of each phase (the unit and web suites), `full` at the end of each stage.
  if [ "${DISPATCH_TIER:-full}" = phase ]; then tier=phase; reason="dispatched: the phase run (unit and web suites)"
  else tier=full; reason="dispatched: the full suite"; fi
  everything=true
elif [ "$event" != pull_request ]; then
  # The owner, 18 November 2026: main gets the same quick checks as a pull request; more runs only when dispatched.
  tier=light; everything=true; reason="a push to main: builds and type checks only"
elif [ "$known" != true ]; then
  tier=full; everything=true; reason="the list of changed files isn't complete (the comparison failed, or has 300 files or more)"
elif [ -n "$workflow" ]; then
  # Never the full suite by itself (the owner, 18 November 2026: it runs only when asked): both sides' quick checks.
  tier=light; everything=true; reason="$workflow changed: both sides' builds and type checks"
elif [ "$web$dotnet$image$deploy" = falsefalsefalsefalse ]; then
  tier=none; reason="only docs changed (docs/, a Markdown file at the root, tools/ui-parity/)"
elif has ci:full; then
  tier=full; reason="the ci:full label"
elif has ci:none && [ "$image$deploy" = falsefalse ]; then
  tier=none; reason="the ci:none label"
else
  tier=light; reason="no ci: label: the default"
  if has ci:none; then reason="the ci:none label isn't honoured (deploy/, the Dockerfile or a package manifest changed): the default"; fi
fi
[ -z "$unknown" ] || reason="$reason; $unknown matches no path rule, so both sides get their checks"

# ---- The jobs. ----
web_job=none; dotnet_job=false; integration=false; e2e=false; image_job=false; helm=false; coverage=false
case "$tier" in
  full)
    if [ "$everything" = true ]; then web=true; dotnet=true; deploy=true; fi
    # The full web checks always include the production build.
    if [ "$web" = true ]; then web_job=full; web_build=true; fi
    dotnet_job=$dotnet; integration=$dotnet; helm=$deploy
    # The image and the E2E tests are of both sides together.
    if [ "$web" = true ] || [ "$dotnet" = true ]; then e2e=true; image=true; fi
    image_job=$image
    # The sticky coverage comment is a pull request's.
    [ "$event" != pull_request ] || coverage=true
    ;;
  phase)
    web_job=full; web_build=true; dotnet_job=true
    ;;
  light)
    # Builds, lint and type checks only: no tests (the owner, 18 November 2026).
    if [ "$everything" = true ]; then web=true; dotnet=true; fi
    [ "$web" = false ] || web_job=light
    dotnet_job=$dotnet; image_job=$image; helm=$deploy
    ;;
esac
[ "$web_job" != none ] || web_build=false

printf '%s\n' "tier=$tier" "reason=$reason" "web=$web_job" "web_build=$web_build" "dotnet=$dotnet_job" \
  "integration=$integration" "image=$image_job" "e2e=$e2e" "helm=$helm" "coverage=$coverage"
