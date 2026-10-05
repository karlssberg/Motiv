#!/usr/bin/env bash
# Works out what the mutation PR gate mutates, and what each changed file must reach, from the
# src/Motiv C# files that differ between <base> and <head>. Prints one JSON object:
#   { files: [path...], globs: [--mutate glob...], areas: [{name, baseline}...],
#     thresholds: [{file, score, survivors}...] }
# Each file is placed in the shard (area) of test/Motiv.Tests/stryker-shards.json whose directory it
# sits under — the same rule plan-shards.sh uses — or in "rest". gate.sh then checks each file on its
# own: it passes when it scores at least `score`, its area's baseline
# (test/Motiv.Tests/stryker-baselines.json). A file whose last measured score
# (test/Motiv.Tests/stryker-file-baselines.json) is below that baseline may pass instead by having no
# more undetected mutants than were recorded, `survivors`; for every other file `survivors` is null.
# Usage: pr-scope.sh <base> <head> [shards.json] [baselines.json] [file-baselines.json]
set -euo pipefail

base="$1"
head="$2"
shards="${3:-test/Motiv.Tests/stryker-shards.json}"
baselines="${4:-test/Motiv.Tests/stryker-baselines.json}"
file_baselines="${5:-test/Motiv.Tests/stryker-file-baselines.json}"

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

jq -n --slurpfile shards "$shards" --slurpfile baselines "$baselines" \
  --slurpfile file_baselines "$file_baselines" '
  $ARGS.positional as $files
  | ($shards[0] | to_entries) as $shards
  | $baselines[0] as $baselines
  | $file_baselines[0] as $file_baselines
  # Directories a file sits in: every path segment but the file name.
  | def dirs: split("/")[:-1];
    def areas: dirs as $d | [ $shards[] | select(any(.value[]; IN($d[]))) | .key ]
      | if length == 0 then [ "rest" ] else . end;
  ([ $files[] | areas[] ] | unique) as $touched
  | ([ $touched[] | select($baselines[.] == null) ]) as $unknown
  | if ($unknown | length) > 0 then
      error("no baseline in stryker-baselines.json for area(s): \($unknown | join(", "))")
    else . end
  # A file already below its area may instead keep to the undetected mutants it was recorded with.
  | { files: $files,
      globs: [ $files[] | "**/\(.)" ],
      areas: [ $touched[] | { name: ., baseline: $baselines[.] } ],
      thresholds: [ $files[] | ([ areas[] | $baselines[.] ] | min) as $area | $file_baselines[.] as $own
        | { file: ., score: $area,
            survivors: (if $own != null and $own.score < $area then $own.survivors else null end) } ] }
' --args "${files[@]}"
