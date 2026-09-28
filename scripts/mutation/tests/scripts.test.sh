#!/usr/bin/env bash
# Tests for the scripts under scripts/mutation, against fabricated mutation reports and the
# checked-in shard files.
#
# survivors.sh and summarise.sh read the mutation-testing-report-schema JSON that Stryker.NET and
# StrykerJS both write, but the two spell file paths differently: Stryker.NET keys files by absolute
# path, StrykerJS by a path relative to the package it ran in. These tests pin how each is shortened,
# so a survivor line names a repository-relative path whichever tool wrote the report. plan.sh is
# checked against the shard files it reads. No Stryker needed — only jq.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SURVIVORS="$HERE/../survivors.sh"
SUMMARISE="$HERE/../summarise.sh"
PLAN="$HERE/../plan.sh"

PASS=0
FAIL=0

fail() {
  FAIL=$((FAIL + 1))
  printf '  \033[31mFAIL\033[0m %s\n' "$1"
  [ $# -gt 1 ] && printf '       %s\n' "$2"
  return 0
}

pass() {
  PASS=$((PASS + 1))
  printf '  \033[32mok\033[0m   %s\n' "$1"
}

assert_equals() {
  local actual=$1 expected=$2 what=$3
  if [ "$actual" = "$expected" ]; then
    pass "$what"
  else
    fail "$what" "expected [$expected], got [$actual]"
  fi
}

assert_contains() {
  local haystack=$1 needle=$2 what=$3
  case "$haystack" in
    *"$needle"*) pass "$what" ;;
    *) fail "$what" "expected to contain: $needle" ;;
  esac
}

SANDBOX="$(mktemp -d "${TMPDIR:-/tmp}/motiv-mutation-scripts.XXXXXX")"
trap 'rm -rf "$SANDBOX"' EXIT

# Writes a one-file report: report <out> <file key> <status>...  — one mutant per status, on
# successive lines.
report() {
  local out=$1 key=$2
  shift 2
  jq -n --arg key "$key" --args '
    { schemaVersion: "1", thresholds: { high: 80, low: 60 },
      files: { ($key): { language: "cs", source: "",
        mutants: [ $ARGS.positional | to_entries[]
          | { id: (.key | tostring), mutatorName: "Equality", replacement: "a  !=\n b",
              status: .value,
              location: { start: { line: (.key + 1), column: 5 }, end: { line: (.key + 1), column: 9 } } } ] } } }
  ' "$@" > "$out"
}

echo "survivors.sh"

report "$SANDBOX/net.json" "/home/runner/work/Motiv/Motiv/src/Motiv/OrElse/OrElsePolicy.cs" Killed Survived
assert_equals "$("$SURVIVORS" "$SANDBOX/net.json")" \
  "$(printf 'Survived\tsrc/Motiv/OrElse/OrElsePolicy.cs:2:5\tEquality\t-> a != b')" \
  "shortens a Stryker.NET absolute path to start at src/"

report "$SANDBOX/js-abs.json" "/home/runner/work/Motiv/Motiv/ui/packages/rules-core/src/mutations.ts" NoCoverage
assert_equals "$("$SURVIVORS" "$SANDBOX/js-abs.json")" \
  "$(printf 'NoCoverage\tui/packages/rules-core/src/mutations.ts:1:5\tEquality\t-> a != b')" \
  "keeps the ui/ package prefix of an absolute StrykerJS path, not just its inner src/"

report "$SANDBOX/js-rel.json" "src/mutations.ts" Survived
assert_equals "$("$SURVIVORS" --prefix ui/packages/rules-core "$SANDBOX/js-rel.json")" \
  "$(printf 'Survived\tui/packages/rules-core/src/mutations.ts:1:5\tEquality\t-> a != b')" \
  "--prefix roots a relative StrykerJS path at its package"

assert_equals "$("$SURVIVORS" --prefix ui/packages/rules-core "$SANDBOX/net.json")" \
  "$(printf 'Survived\tsrc/Motiv/OrElse/OrElsePolicy.cs:2:5\tEquality\t-> a != b')" \
  "--prefix leaves an absolute path alone"

report "$SANDBOX/gui.json" "/home/me/gui/Motiv/src/Motiv/Not.cs" Survived
assert_equals "$("$SURVIVORS" "$SANDBOX/gui.json")" \
  "$(printf 'Survived\tsrc/Motiv/Not.cs:1:5\tEquality\t-> a != b')" \
  "does not mistake a directory ending in ui/ for the ui/ root"

assert_equals "$("$SURVIVORS" "$SANDBOX/net.json" "$SANDBOX/js-abs.json" | cut -f1)" \
  "$(printf 'Survived\nNoCoverage')" \
  "lists survivors across several reports, sorted by path"

echo "summarise.sh"

out="$("$SUMMARISE" "Score" "$SANDBOX/net.json" "$SANDBOX/js-abs.json")"
assert_contains "$out" "**33.33%** (1 detected of 3 valid mutants)" "scores the union of a .NET and a JS report"

out="$("$SUMMARISE" "Nothing")"
assert_contains "$out" "No mutation report was written." "says so when there is no report"

echo "plan.sh"

# plan.sh reads the checked-in shard files by repository-relative path, as the workflow runs it.
cd "$HERE/../../.." || exit 1

pr="$("$PLAN")"
assert_equals "$(jq -r '[.include[].project] | unique | join(",")' <<< "$pr")" "motiv" \
  "plans src/Motiv alone without --full"
assert_equals "$(jq -c '.include | map(.name)' <<< "$pr")" \
  "$(scripts/mutation/plan-shards.sh | jq -c '.include | map(.name)')" \
  "keeps every src/Motiv shard plan-shards.sh plans, rest included"

full="$("$PLAN" --full)"
assert_equals "$(jq -r '[.include[].project] | unique | join(",")' <<< "$full")" \
  "analyzer,codefix,motiv,serialization" \
  "--full adds Motiv.Serialization, Motiv.Analyzer and Motiv.CodeFix"
assert_equals "$(jq -r '.include[] | select(.project == "analyzer") | "\(.dir) \(.src) \(.name) \(.globs | length)"' <<< "$full")" \
  "test/Motiv.Analyzer.Tests src/Motiv.Analyzer all 0" \
  "an unsharded project runs as one 'all' shard with no --mutate globs"
assert_equals "$(jq -r '[.include[] | select(.project == "serialization") | .name] | join(",")' <<< "$full")" \
  "propositions,rules,governance,expressions-printing,decisions-diagnostics,rest" \
  "Motiv.Serialization is split by its own stryker-shards.json"
assert_equals "$(jq -r '[.include[] | select(.project == "serialization") | .dir] | unique | join(",")' <<< "$full")" \
  "test/Motiv.Serialization.Tests" \
  "each Serialization shard runs from the Serialization test directory"

for dir in $(jq -r '[.include[].dir] | unique[]' <<< "$full"); do
  if [ -f "$dir/stryker-config.json" ]; then
    pass "$dir has a stryker-config.json to run"
  else
    fail "$dir has a stryker-config.json to run"
  fi
done

printf '\n%d passed, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
