#!/usr/bin/env bash
# Lists the mutants the suite did not detect (Survived and NoCoverage) in mutation-report JSON files
# (the mutation-testing-report-schema both Stryker.NET and StrykerJS write), one per line, sorted by
# file and line:
#   <Status>  <path>:<line>:<column>  <Mutator>  -> <replacement>
# Paths are shortened to start at the repository's src/ or ui/ directory. Stryker.NET keys files by
# absolute path; StrykerJS by a path relative to the package it ran in, so pass that package's
# directory as --prefix to root those at the repository too. Replacements are collapsed to one line
# and truncated.
# Usage: survivors.sh [--prefix <dir>] <report.json>...
set -euo pipefail

prefix=""
if [ "${1:-}" = "--prefix" ]; then
  prefix="${2%/}/"
  shift 2
fi

jq -rs --arg prefix "$prefix" '
  [ .[].files | to_entries[]
    | (.key
       | if startswith("/") or $prefix == "" then . else $prefix + . end
       # The first src/ or ui/ that is a whole path segment: the ui/ of ui/packages/… wins over the
       # src/ inside that package, and a directory merely ending in "ui" does not count.
       | sub("^(?:.*?/)??(?=(?:src|ui)/)"; "")) as $path
    | .value.mutants[]
    | select(.status == "Survived" or .status == "NoCoverage")
    | { $path, line: .location.start.line, column: .location.start.column,
        status, mutator: .mutatorName,
        replacement: (.replacement | gsub("\\s+"; " ") | if length > 100 then .[:100] + "…" else . end) } ]
  | sort_by(.path, .line, .column)[]
  | "\(.status)\t\(.path):\(.line):\(.column)\t\(.mutator)\t-> \(.replacement)"
' "$@"
