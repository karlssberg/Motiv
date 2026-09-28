#!/usr/bin/env bash
# Emits the mutation workflow's Stryker.NET job matrix: one entry per shard of each mutated project,
# each naming the project (`project`), the test directory Stryker runs from (`dir`), the source it
# mutates (`src`), the shard (`name`) and its --mutate globs (`globs`, empty for the whole project).
#
# A project with a stryker-shards.json is split by plan-shards.sh; one without runs as a single
# "all" shard. Without --full only src/Motiv is planned — what a pull request to the mutation setup
# pays for. --full adds the projects the scheduled run covers (#294 step 4).
# Usage: plan.sh [--full]
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# plan <project> <test dir> <mutated source dir>
plan() {
  local shards="$2/stryker-shards.json"
  if [ -f "$shards" ]; then
    "$here/plan-shards.sh" "$shards"
  else
    echo '{ "include": [ { "name": "all", "globs": [] } ] }'
  fi | jq -c --arg project "$1" --arg dir "$2" --arg src "$3" \
    '.include | map({ project: $project, dir: $dir, src: $src } + .)'
}

{
  plan motiv test/Motiv.Tests src/Motiv
  if [ "${1:-}" = "--full" ]; then
    plan serialization test/Motiv.Serialization.Tests src/Motiv.Serialization
    plan analyzer test/Motiv.Analyzer.Tests src/Motiv.Analyzer
    plan codefix test/Motiv.CodeFix.Tests src/Motiv.CodeFix
  fi
} | jq -cs '{ include: add }'
