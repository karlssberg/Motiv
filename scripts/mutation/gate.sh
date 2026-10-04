#!/usr/bin/env bash
# The mutation PR gate's verdict: checks each changed file in a Stryker.NET mutation report against
# its threshold from pr-scope.sh, on its own, and writes a Markdown table of the result. A file
# passes when it scores at least its threshold's `score`, or, when `survivors` is set, when it has
# no more undetected mutants (Survived + NoCoverage) than that. A file with no valid mutants is not
# held to anything. Exits 1 when any file fails, or when no report exists.
# Usage: gate.sh <scope.json> <report.json>...
set -euo pipefail

scope="$1"
shift

reports=()
for report in "$@"; do
  if [ -f "$report" ]; then reports+=("$report"); fi
done
if [ "${#reports[@]}" -eq 0 ]; then
  echo "gate.sh: no mutation report to check." >&2
  exit 1
fi

measured="$("$(dirname "${BASH_SOURCE[0]}")/file-scores.sh" "${reports[@]}")"

jq -r --argjson measured "$measured" '
  def pct: "\(.)%";
  [ .thresholds[] | . as $t | $measured[.file] as $m
    | { file,
        needed: ((.score | pct)
                 + (if .survivors == null then "" else ", or at most \(.survivors) undetected" end)),
        got: (if $m == null then "no valid mutants"
              else ($m.score | pct) + (if $t.survivors == null then "" else ", \($m.survivors) undetected" end)
              end),
        result: (if $m == null then "not applied"
                 elif $m.score >= $t.score or ($t.survivors != null and $m.survivors <= $t.survivors)
                 then "pass"
                 else "**fail**" end) } ] as $rows
  | "| File | Measured | Needed | Result |\n|---|---|---|---|\n"
    + ($rows | map("| `\(.file)` | \(.got) | \(.needed) | \(.result) |") | join("\n")),
    (if any($rows[]; .result == "**fail**")
     then "gate.sh: a changed file is below its threshold.\n" | halt_error(1) else empty end)
' "$scope"
