#!/usr/bin/env bash
# Works out what the mutation PR gate mutates, and the score it must reach, from the src/Motiv C#
# files that differ between <base> and <head>. Prints one JSON object:
#   { files: [path...], globs: [--mutate glob...], areas: [{name, baseline}...], break: int|null }
# Each file is placed in the shard (area) of test/Motiv.Tests/stryker-shards.json whose directory it
# sits under — the same rule plan-shards.sh uses — or in "rest". `break` is the lowest post-triage
# baseline (test/Motiv.Tests/stryker-baselines.json) among the areas touched, rounded down because
# Stryker's --break-at takes a whole number. No changed files gives `break: null`.
# Usage: pr-scope.sh <base> <head> [shards.json] [baselines.json]
set -euo pipefail

base="$1"
head="$2"
shards="${3:-test/Motiv.Tests/stryker-shards.json}"
baselines="${4:-test/Motiv.Tests/stryker-baselines.json}"

# Added, copied and modified paths (with --no-renames a rename is an add); a deleted file has nothing
# left to mutate.
mapfile -t files < <(git diff --name-only --no-renames --diff-filter=ACM "$base" "$head" -- ':(glob)src/Motiv/**/*.cs')

# A path is passed to Stryker as a glob, so a glob metacharacter in it would widen or break the match.
for file in "${files[@]}"; do
  if [[ "$file" == *[][{}?*!,]* ]]; then
    echo "pr-scope.sh: '$file' contains a glob metacharacter; it cannot be passed to --mutate as is." >&2
    exit 1
  fi
done

jq -n --slurpfile shards "$shards" --slurpfile baselines "$baselines" '
  $ARGS.positional as $files
  | ($shards[0] | to_entries) as $shards
  | $baselines[0] as $baselines
  # Directories a file sits in: every path segment but the file name.
  | def dirs: split("/")[:-1];
    def areas: dirs as $d | [ $shards[] | select(any(.value[]; IN($d[]))) | .key ]
      | if length == 0 then [ "rest" ] else . end;
  ([ $files[] | areas[] ] | unique) as $touched
  | ([ $touched[] | select($baselines[.] == null) ]) as $unknown
  | if ($unknown | length) > 0 then
      error("no baseline in stryker-baselines.json for area(s): \($unknown | join(", "))")
    else . end
  | { files: $files,
      globs: [ $files[] | "**/\(.)" ],
      areas: [ $touched[] | { name: ., baseline: $baselines[.] } ],
      break: (if ($touched | length) == 0 then null
              else [ $touched[] | $baselines[.] ] | min | floor end) }
' --args "${files[@]}"
