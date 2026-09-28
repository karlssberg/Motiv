#!/usr/bin/env bash
# Lists the mutants the suite did not detect (Survived and NoCoverage) in mutation-report JSON files
# (the mutation-testing-report-schema both Stryker.NET and StrykerJS write), one per line, sorted by
# file and line:
#   <Status>  <path>:<line>:<column>  <Mutator>  -> <replacement>
# Paths are shortened to start at the repository's src/ or ui/ directory. Stryker.NET keys files by
# absolute path; StrykerJS by a path relative to the package it ran in, so pass that package's
# directory as --prefix to root those at the repository too. Replacements are collapsed to one line
# and truncated.
#
# --details is for a CI job: it prints the list to stderr, for the job log, and a collapsed Markdown
# <details> block holding it to stdout, for the job summary. A report path that does not exist is
# skipped, so a run that wrote no report lists nothing.
# Usage: survivors.sh [--prefix <dir>] [--details] <report.json>...
set -euo pipefail

prefix=""
details=false
while [ "$#" -gt 0 ]; do
  case "$1" in
    --prefix) prefix="${2%/}/"; shift 2 ;;
    --details) details=true; shift ;;
    *) break ;;
  esac
done

reports=()
for report in "$@"; do
  if [ -f "$report" ]; then reports+=("$report"); fi
done
[ "${#reports[@]}" -gt 0 ] || exit 0

list() {
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
  ' "${reports[@]}"
}

if [ "$details" = false ]; then
  list
  exit 0
fi

survivors="$(list)"
if [ -n "$survivors" ]; then
  printf '%s\n' "$survivors" >&2
  count="$(printf '%s\n' "$survivors" | wc -l)"
else
  count=0
fi
printf '\n<details><summary>%s undetected mutants (Survived, NoCoverage)</summary>\n\n```\n' "$count"
if [ -n "$survivors" ]; then printf '%s\n' "$survivors"; fi
printf '```\n\n</details>\n'
