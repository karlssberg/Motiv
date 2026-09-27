#!/usr/bin/env bash
# Lists the mutants the suite did not detect (Survived and NoCoverage) in Stryker.NET
# mutation-report.json files, one per line, sorted by file and line:
#   <Status>  <path>:<line>:<column>  <Mutator>  -> <replacement>
# Paths are shortened to start at src/; replacements are collapsed to one line and truncated.
# Usage: survivors.sh <report.json>...
set -euo pipefail

jq -rs '
  [ .[].files | to_entries[]
    | (.key | sub("^.*?(?=src/)"; "")) as $path
    | .value.mutants[]
    | select(.status == "Survived" or .status == "NoCoverage")
    | { $path, line: .location.start.line, column: .location.start.column,
        status, mutator: .mutatorName,
        replacement: (.replacement | gsub("\\s+"; " ") | if length > 100 then .[:100] + "…" else . end) } ]
  | sort_by(.path, .line, .column)[]
  | "\(.status)\t\(.path):\(.line):\(.column)\t\(.mutator)\t-> \(.replacement)"
' "$@"
