#!/usr/bin/env bash
# Writes the mutation workflow's combined job summary: one Markdown section per Stryker.NET project
# in the plan matrix, in plan order, scoring the union of the shard reports under <reports dir> (as
# actions/download-artifact lays them out: <dir>/mutation-report-<project>-<shard>/reports/…), and
# naming any shard that sent no report.
# Usage: combine.sh <matrix JSON, as plan.sh prints it> <reports dir>
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
matrix="$1"
dir="$2"

# Projects in plan order, each once.
for project in $(jq -r '.include[].project' <<< "$matrix" | awk '!seen[$0]++'); do
  src=$(jq -r --arg p "$project" 'first(.include[] | select(.project == $p)) | .src' <<< "$matrix")
  reports=()
  missing=()
  for shard in $(jq -r --arg p "$project" '.include[] | select(.project == $p) | .name' <<< "$matrix"); do
    report="$dir/mutation-report-$project-$shard/reports/mutation-report.json"
    if [ -f "$report" ]; then reports+=("$report"); else missing+=("$shard"); fi
  done
  "$here/summarise.sh" "Mutation score — $src (all shards)" "${reports[@]}"
  if [ "${#missing[@]}" -gt 0 ]; then
    printf '\n**Incomplete:** no report from %s. The score above covers the other shards only.\n' "${missing[*]}"
  fi
  printf '\n'
done
