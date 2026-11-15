#!/usr/bin/env bash
# Deletes the local kind cluster, and with it the cluster's database. The AppHost's dev database isn't touched.
set -euo pipefail

if ! command -v kind >/dev/null && [[ -n ${LOCALAPPDATA:-} ]]; then
  PATH="$PATH:$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/Microsoft/WinGet/Links"
fi

kind delete cluster --name medapp
