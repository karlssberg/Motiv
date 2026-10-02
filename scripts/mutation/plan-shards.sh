#!/usr/bin/env bash
# Turns test/Motiv.Tests/stryker-shards.json into the mutation workflow's job matrix: one shard per
# entry, mutating the named src/Motiv directories, plus a "rest" shard that excludes every one of
# them — so a file no shard names is still mutated, exactly once.
set -euo pipefail

shards="${1:-test/Motiv.Tests/stryker-shards.json}"

jq -c '
  [ to_entries[] | { name: .key, globs: [ .value[] | "**/\(.)/**" ] } ]
  + [ { name: "rest", globs: [ .[][] | "!**/\(.)/**" ] } ]
  | { include: . }
' "$shards"
