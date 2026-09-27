#!/usr/bin/env bash
# Writes a Markdown mutation-score summary for one or more Stryker.NET mutation-report.json files.
# Usage: summarise.sh <title> <report.json>...
# The score is Stryker's own formula: detected (Killed + Timeout) over valid (detected + Survived +
# NoCoverage). With several reports the score is over their union, as if one run had produced them.
set -euo pipefail

title="$1"
shift

if [ "$#" -eq 0 ]; then
  printf '## %s\n\nNo mutation report was written.\n' "$title"
  exit 0
fi

jq -rs --arg title "$title" '
  [ .[].files[].mutants[].status ] as $s
  | def n($k): [ $s[] | select(. == $k) ] | length;
    (n("Killed") + n("Timeout")) as $detected
  | ($detected + n("Survived") + n("NoCoverage")) as $valid
  | "## \($title)\n\n"
    + "**\(if $valid == 0 then "n/a" else (($detected * 10000 / $valid | round) / 100 | tostring) + "%" end)** "
    + "(\($detected) detected of \($valid) valid mutants)\n\n"
    + "| Status | Mutants |\n|---|---|\n"
    + ([ "Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored" ]
       | map("| \(.) | \(n(.)) |") | join("\n"))
    + "\n"
' "$@"
