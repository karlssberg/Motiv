#!/usr/bin/env bash
# Writes a Markdown mutation-score summary for one or more Stryker.NET mutation-report.json files.
# Usage: summarise.sh <title> <report.json>...
#        summarise.sh --valid <report.json>...   prints only the number of valid mutants
# The score is Stryker's own formula: detected (Killed + Timeout) over valid (detected + Survived +
# NoCoverage). With several reports the score is over their union, as if one run had produced them.
# A report path that does not exist is skipped; with none left, the summary says no report was
# written.
set -euo pipefail

mode=summary
title=""
if [ "${1:-}" = "--valid" ]; then
  mode=valid
else
  title="$1"
fi
shift

reports=()
for report in "$@"; do
  if [ -f "$report" ]; then reports+=("$report"); fi
done

if [ "${#reports[@]}" -eq 0 ]; then
  if [ "$mode" = valid ]; then echo 0; else printf '## %s\n\nNo mutation report was written.\n' "$title"; fi
  exit 0
fi

jq -rs --arg title "$title" --arg mode "$mode" '
  [ .[].files[].mutants[].status ] as $s
  | def n($k): [ $s[] | select(. == $k) ] | length;
    (n("Killed") + n("Timeout")) as $detected
  | ($detected + n("Survived") + n("NoCoverage")) as $valid
  | if $mode == "valid" then $valid else
    "## \($title)\n\n"
    + "**\(if $valid == 0 then "n/a" else (($detected * 10000 / $valid | round) / 100 | tostring) + "%" end)** "
    + "(\($detected) detected of \($valid) valid mutants)\n\n"
    + "| Status | Mutants |\n|---|---|\n"
    + ([ "Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored" ]
       | map("| \(.) | \(n(.)) |") | join("\n"))
    + "\n"
    end
' "${reports[@]}"
