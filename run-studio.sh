#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
echo "==> Building the UI"
pnpm -C ui install --frozen-lockfile
pnpm -C ui/apps/studio build
echo "==> Starting the host on http://localhost:5100"
ASPNETCORE_ENVIRONMENT=Development exec dotnet run --project src/Motiv.Studio --urls http://localhost:5100
