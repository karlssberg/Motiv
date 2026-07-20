#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
echo "==> Building the UI"
pnpm -C ui install --frozen-lockfile
pnpm -C ui/apps/demo build
echo "==> Starting the host on http://localhost:5100"
exec dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100
