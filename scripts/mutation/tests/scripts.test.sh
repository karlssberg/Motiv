#!/usr/bin/env bash
# Tests for the scripts under scripts/mutation, against fabricated mutation reports and the
# checked-in shard files.
#
# survivors.sh and summarise.sh read the mutation-testing-report-schema JSON that Stryker.NET and
# StrykerJS both write, but the two spell file paths differently: Stryker.NET keys files by absolute
# path, StrykerJS by a path relative to the package it ran in. These tests pin how each is shortened,
# so a survivor line names a repository-relative path whichever tool wrote the report. plan.sh is
# checked against the shard files it reads, combine.sh against a fabricated artifact tree, and
# pr-scope.sh against a throwaway git repository. No Stryker needed — only jq and git.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SURVIVORS="$HERE/../survivors.sh"
SUMMARISE="$HERE/../summarise.sh"
PLAN="$HERE/../plan.sh"
COMBINE="$HERE/../combine.sh"
PR_SCOPE="$HERE/../pr-scope.sh"

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

details_err="$("$SURVIVORS" --details "$SANDBOX/net.json" 2>&1 >/dev/null)"
assert_equals "$details_err" \
  "$(printf 'Survived\tsrc/Motiv/OrElse/OrElsePolicy.cs:2:5\tEquality\t-> a != b')" \
  "--details prints the survivor list to stderr, for the job log"
# shellcheck disable=SC2016 # the backticks are Markdown, not a command substitution
assert_equals "$("$SURVIVORS" --details "$SANDBOX/net.json" 2>/dev/null)" \
  "$(printf '\n<details><summary>1 undetected mutants (Survived, NoCoverage)</summary>\n\n```\nSurvived\tsrc/Motiv/OrElse/OrElsePolicy.cs:2:5\tEquality\t-> a != b\n```\n\n</details>')" \
  "--details prints a collapsed summary block to stdout"
assert_equals "$("$SURVIVORS" --prefix ui/packages/rules-core --details "$SANDBOX/js-rel.json" 2>/dev/null | sed -n 5p)" \
  "$(printf 'Survived\tui/packages/rules-core/src/mutations.ts:1:5\tEquality\t-> a != b')" \
  "--details combines with --prefix"
report "$SANDBOX/none.json" "/w/src/Motiv/A.cs" Killed
assert_contains "$("$SURVIVORS" --details "$SANDBOX/none.json" 2>/dev/null)" \
  "<summary>0 undetected mutants" "--details still writes the block when nothing survived"
out="$("$SURVIVORS" --details "$SANDBOX/missing.json" 2>&1)"
assert_equals "$?:$out" "0:" "--details writes nothing, and succeeds, when the report is missing"

echo "summarise.sh"

out="$("$SUMMARISE" "Score" "$SANDBOX/net.json" "$SANDBOX/js-abs.json")"
assert_contains "$out" "**33.33%** (1 detected of 3 valid mutants)" "scores the union of a .NET and a JS report"

out="$("$SUMMARISE" "Nothing")"
assert_contains "$out" "No mutation report was written." "says so when there is no report"

out="$("$SUMMARISE" "Missing" "$SANDBOX/missing.json")"
assert_contains "$out" "No mutation report was written." "treats a report path that does not exist as no report"

out="$("$SUMMARISE" "Some" "$SANDBOX/net.json" "$SANDBOX/missing.json")"
assert_contains "$out" "**50%** (1 detected of 2 valid mutants)" "scores the reports that exist and skips the rest"

assert_equals "$("$SUMMARISE" --valid "$SANDBOX/net.json" "$SANDBOX/js-abs.json")" "3" \
  "--valid prints the number of valid mutants"
report "$SANDBOX/invalid.json" "/w/src/Motiv/A.cs" CompileError Ignored
assert_equals "$("$SUMMARISE" --valid "$SANDBOX/invalid.json")" "0" \
  "--valid counts no CompileError or Ignored mutant as valid"

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

echo "combine.sh"

matrix='{"include":[
  {"project":"motiv","src":"src/Motiv","name":"ops"},
  {"project":"motiv","src":"src/Motiv","name":"rest"},
  {"project":"analyzer","src":"src/Motiv.Analyzer","name":"all"}]}'
mkdir -p "$SANDBOX/artifacts/mutation-report-motiv-ops/reports" \
         "$SANDBOX/artifacts/mutation-report-motiv-rest/reports" \
         "$SANDBOX/artifacts/mutation-js-report-rules-core"
cp "$SANDBOX/net.json" "$SANDBOX/artifacts/mutation-report-motiv-ops/reports/mutation-report.json"
cp "$SANDBOX/js-abs.json" "$SANDBOX/artifacts/mutation-report-motiv-rest/reports/mutation-report.json"
out="$("$COMBINE" "$matrix" "$SANDBOX/artifacts")"
assert_equals "$(grep '^## ' <<< "$out")" \
  "$(printf '## Mutation score — src/Motiv (all shards)\n## Mutation score — src/Motiv.Analyzer (all shards)')" \
  "writes one section per project, in plan order"
assert_contains "$out" "**33.33%** (1 detected of 3 valid mutants)" "scores a project over the union of its shards"
assert_contains "$out" "$(printf '## Mutation score — src/Motiv.Analyzer (all shards)\n\nNo mutation report was written.\n\n**Incomplete:** no report from all.')" \
  "names a project's shards that sent no report"
assert_equals "$(grep -c 'Incomplete' <<< "$out")" "1" "does not call a project with every shard reported incomplete"

rm "$SANDBOX/artifacts/mutation-report-motiv-rest/reports/mutation-report.json"
assert_contains "$("$COMBINE" "$matrix" "$SANDBOX/artifacts")" \
  "$(printf '**50%%** (1 detected of 2 valid mutants)\n\n| Status')" \
  "scores the shards that did report"
assert_contains "$("$COMBINE" "$matrix" "$SANDBOX/artifacts")" \
  "**Incomplete:** no report from rest." "names the one missing shard"

echo "pr-scope.sh"

# A throwaway repository: pr-scope.sh diffs two commits of whatever repository it runs in.
REPO="$SANDBOX/repo"
git init -q "$REPO"
git -C "$REPO" config user.email test@example.invalid
git -C "$REPO" config user.name test
git -C "$REPO" config commit.gpgsign false
mkdir -p "$REPO/src/Motiv/Not" "$REPO/src/Motiv/OrElse/Nested" "$REPO/src/Motiv/HigherOrderProposition"
for f in src/Motiv/Spec.cs src/Motiv/Not/NotPolicy.cs src/Motiv/OrElse/Nested/Deep.cs \
         src/Motiv/HigherOrderProposition/All.cs src/Motiv/Gone.cs; do
  echo "// v1" > "$REPO/$f"
done
git -C "$REPO" add -A
git -C "$REPO" commit -qm base
BASE_SHA="$(git -C "$REPO" rev-parse HEAD)"

echo '{ "operators": ["Not", "OrElse"], "higher-order": ["HigherOrderProposition"] }' > "$SANDBOX/shards.json"
echo '{ "operators": 72.46, "higher-order": 39.85, "rest": 75.7 }' > "$SANDBOX/baselines.json"
echo '{ "operators": 72.46, "higher-order": 39.85 }' > "$SANDBOX/no-rest.json"

# scope <head> [baselines]: pr-scope.sh from base to <head>, against the sandbox shard files.
scope() {
  (cd "$REPO" && "$PR_SCOPE" "$BASE_SHA" "$1" "$SANDBOX/shards.json" "${2:-$SANDBOX/baselines.json}")
}

# commit <message> <command>...: runs the commands in the repository, then commits from the base.
commit() {
  local message=$1
  shift
  git -C "$REPO" checkout -q "$BASE_SHA"
  (cd "$REPO" && "$@")
  git -C "$REPO" add -A
  git -C "$REPO" commit -qm "$message"
  git -C "$REPO" rev-parse HEAD
}

empty="$(commit empty touch README.md)"
assert_equals "$(scope "$empty" | jq -c .)" '{"files":[],"globs":[],"areas":[],"break":null}' \
  "no changed src/Motiv C# file gives nothing to mutate and break: null"

rest="$(commit rest sh -c 'echo "// v2" >> src/Motiv/Spec.cs')"
assert_equals "$(scope "$rest" | jq -c .)" \
  '{"files":["src/Motiv/Spec.cs"],"globs":["**/src/Motiv/Spec.cs"],"areas":[{"name":"rest","baseline":75.7}],"break":75}' \
  "a file under no shard directory falls in rest"

nested="$(commit nested sh -c 'echo "// v2" >> src/Motiv/OrElse/Nested/Deep.cs')"
assert_equals "$(scope "$nested" | jq -c '[.areas, .break]')" '[[{"name":"operators","baseline":72.46}],72]' \
  "a file anywhere beneath a shard directory is in that shard"

several="$(commit several sh -c 'echo "// v2" | tee -a src/Motiv/Not/NotPolicy.cs src/Motiv/HigherOrderProposition/All.cs src/Motiv/Spec.cs > /dev/null')"
assert_equals "$(scope "$several" | jq -c '[[.areas[].name], .break]')" '[["higher-order","operators","rest"],39]' \
  "several areas break at the lowest of their baselines, rounded down"

deleted="$(commit deleted sh -c 'git rm -q src/Motiv/Gone.cs && echo x > src/Motiv/Not/notes.txt && echo "// new" > src/Motiv/Not/Added.cs')"
assert_equals "$(scope "$deleted" | jq -c .files)" '["src/Motiv/Not/Added.cs"]' \
  "mutates added C# files, not deleted ones or other file types"

out="$(scope "$rest" "$SANDBOX/no-rest.json" 2>&1)"
status=$?
assert_equals "$status" "5" "fails when a touched area has no baseline"
assert_contains "$out" "no baseline in stryker-baselines.json for area(s): rest" "names the area with no baseline"

glob="$(commit glob sh -c 'echo "// new" > "src/Motiv/Weird[1].cs"')"
out="$(scope "$glob" 2>&1)"
status=$?
assert_equals "$status" "1" "refuses a changed path with a glob metacharacter in it"
assert_contains "$out" "contains a glob metacharacter" "says why it refused the path"

printf '\n%d passed, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
