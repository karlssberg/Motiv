#!/usr/bin/env bash
# Prints each file's mutation score and undetected-mutant count from Stryker.NET mutation-report.json
# files, as one JSON object keyed by the file's path from the repository's src/ directory:
#   { "src/Motiv/Not/NotSpec.cs": { "score": 80, "survivors": 3 }, ... }
# The score is Stryker's own formula, detected (Killed + Timeout) over valid (detected + Survived +
# NoCoverage), rounded down to two decimals so a file measured at exactly that score never falls
# below it. `survivors` counts the undetected mutants (Survived + NoCoverage). Files with no valid
# mutant are left out; a report path that does not exist is skipped.
# This is how test/Motiv.Tests/stryker-file-baselines.json is written from a full run's shard
# reports; see docs/contributing/mutation-testing.md, "What each file must reach".
# Usage: file-scores.sh <report.json>...
set -euo pipefail

reports=()
for report in "$@"; do
  if [ -f "$report" ]; then reports+=("$report"); fi
done
[ "${#reports[@]}" -gt 0 ] || { echo '{}'; exit 0; }

jq -s '
  [ .[].files | to_entries[]
    | { path: (.key | sub("^(?:.*?/)??(?=src/)"; "")),
        statuses: [ .value.mutants[].status ] } ]
  | group_by(.path)
  | map({ path: .[0].path, statuses: [ .[].statuses[] ] }
        | (.statuses | map(select(. == "Killed" or . == "Timeout")) | length) as $detected
        | (.statuses | map(select(. == "Survived" or . == "NoCoverage")) | length) as $undetected
        | select($detected + $undetected > 0)
        | { key: .path,
            value: { score: ($detected * 10000 / ($detected + $undetected) | floor / 100),
                     survivors: $undetected } })
  | sort_by(.key) | from_entries
' "${reports[@]}"
