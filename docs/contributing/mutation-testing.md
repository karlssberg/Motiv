# Mutation testing

Line coverage says which code the suite *executes*; mutation testing says which behaviour it
*checks*. [Stryker.NET](https://github.com/stryker-mutator/stryker-net) makes small changes
(mutants) to `src/Motiv` — flips a `&&` to `||`, empties a string, removes a statement — and runs
`test/Motiv.Tests` against each one. A mutant the suite still passes against has **survived**: some
behaviour changed and no test noticed. For Motiv that is most often explanation text — a test that
runs a path but asserts only `Satisfied` lets every mutant in `Reason`, `Assertions` or
`Justification` survive.

This is tracked in #294. The full run is **report-only**: nothing fails on a low score. A pull
request that changes `src/Motiv` is gated on the files it changes (see [PR gate](#pr-gate)).

## Running it locally

Needs the .NET 10 runtime (Stryker.NET 5 requires it) and the SDKs `Motiv.Tests` restores for.

```bash
dotnet tool restore            # once; installs the pinned version from .config/dotnet-tools.json
cd test/Motiv.Tests
dotnet tool run dotnet-stryker # reads stryker-config.json in this directory
```

A full run is slow — it runs the covering tests once per mutant. To mutate a subset, pass
`--mutate` (repeatable, a glob over the mutated project's files), for example
`dotnet tool run dotnet-stryker --mutate "**/OrElse*.cs"`.

The run writes to `test/Motiv.Tests/StrykerOutput/<timestamp>/` (git-ignored):
`reports/mutation-report.html` and `reports/mutation-report.json`.

## What the checked-in config decides

`test/Motiv.Tests/stryker-config.json`:

- **`target-framework: net10.0`** — Stryker.NET mutates one framework per run. Code only the
  `netstandard2.0` build compiles (`#if !NET8_0_OR_GREATER` branches, the `ns20asset` leg) is not
  mutated by this run.
- **`coverage-analysis: perTest`** — each mutant runs only the tests that cover it.
- **`thresholds.break: 0`** — the full run is report-only. The PR gate sets its own break on the
  command line (see [PR gate](#pr-gate)).

## Reading the report

Open `mutation-report.html`. Each file shows its mutants inline, each with one status:

| Status | Meaning | What to do |
|---|---|---|
| Killed | A test failed against the mutant. | Nothing. |
| Timeout | The mutant made the tests hang (e.g. an endless loop). Counted as detected. | Nothing. |
| Survived | Every covering test passed. | Write the test that notices, or mark the mutant equivalent. |
| NoCoverage | No test executes this code. | Write a test, or confirm the code is dead. |
| CompileError | The mutant does not compile. Excluded from the score. | Nothing. |
| RuntimeError | The test run itself failed against the mutant. Excluded from the score. | Look at it if many appear. |
| Ignored | Excluded by config or a comment. | Check the reason still holds. |

The score is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`. CI writes it and the
per-status counts to the job summary.

## Marking an equivalent mutant

Some mutants cannot change observable behaviour (an equivalent mutant) — no test can kill them. Mark
them in source with a Stryker comment **and a reason**, so the exclusion is reviewable:

```csharp
// Stryker disable once String: the message is only ever read when the assertion itself fails
Debug.Assert(depth >= 0, "depth went negative");
```

Syntax: `// Stryker [disable|restore] [once] [all | mutator list][: reason]`. `disable once` affects the
next line only; `disable` without `once` lasts until a matching `restore`. Name only the mutators
that are equivalent — the condition on the line above is still mutated, and still has to be
killed. Mutator names are listed in
Stryker.NET's `docs/mutations.md`.

Prefer a test over a comment. An exclusion is right only when you can say why no test could tell
the difference.

The one other reason to exclude a mutant is that it takes the runner down with it. Turning
`IsCollapsable` off on `AndBooleanResult` or `OrElseBooleanResult` makes the 3,000- and 50,000-deep
chains in `Traversal/` build a justification whose memory grows with the cube of its depth. It fills
the runner's memory in seconds, before Stryker's timeout fires, and the job ends with *The runner
has received a shutdown signal* just after *Retrying the test session*. Those two lines are disabled,
and a small test pins each collapse, so the exclusion hides no gap. If a shard dies that way again,
look for another mutant that makes deep-chain work super-linear.

As a backstop, the Stryker steps in both workflows set `DOTNET_GCHeapHardLimit` to 3 GiB, so a
runaway mutant fails its test with an `OutOfMemoryException` and counts as killed rather than taking
the runner down. A local run has no such cap unless you set the same variable.

## CI

`.github/workflows/mutation.yml` runs on demand (`workflow_dispatch`) and on pull requests that
change the mutation setup itself. It splits `src/Motiv` into shards by directory, as listed in
`test/Motiv.Tests/stryker-shards.json`. A final `rest` shard mutates every file the listed shards
leave out, so a new directory is still covered. Each shard uploads its own
`mutation-report-motiv-<shard>` artifact. The `combine` job scores all the shards together and names
any shard that sent no report. Each shard's job summary also lists its undetected mutants
(Survived and NoCoverage) as `path:line:column  mutator  -> replacement`, and prints the same list to
its log, so you can triage without downloading the artifact.

To mutate one shard's files locally, pass the same globs, for example
`dotnet tool run dotnet-stryker --mutate "**/OrElse/**" --mutate "**/AndAlso/**"`.

## PR gate

`.github/workflows/mutation-pr.yml` runs on every pull request to `main` that touches `src/Motiv`,
`test/Motiv.Tests` or the mutation tooling. It mutates **only the `src/Motiv` C# files the pull
request adds or modifies**, runs the whole of `Motiv.Tests` against those mutants, and fails when
their score is below a break threshold. A full run takes about 150 runner-minutes. The gate's cost
grows with the number of files changed.

### What it mutates

`scripts/mutation/pr-scope.sh` diffs the merge commit against its first parent (the `main` it merges
into). It passes each changed file to Stryker as a `--mutate` glob. Scoring is per **file**: every
mutant in a changed file counts, not only the ones on changed lines. Stryker's own `--since` works
at the same granularity.

The gate does not use `--since`, because two rules in Stryker.NET 5.0.0 would turn ordinary pull
requests into full runs:

- A changed file under the test project that is not `.cs` marks **every** mutant as changed. That
  includes `Motiv.Tests.csproj`, `stryker-config.json` and the embedded `DescriptionBaseline.txt`.
- A changed test `.cs` file marks every mutant that a test in that file covers as changed, anywhere
  in `src/Motiv`. If the test adapter reports no source file for a test, Stryker counts that test as
  changed, and every mutant it covers is re-tested.

Stryker applies `--mutate` before `--since`, so using both would reduce to the `--mutate` list and
add nothing.

A pull request that changes only tests has nothing to mutate, so the gate reports that and passes.
Weakened tests show up in the full run, not here.

### The break threshold

Each changed file has a threshold, and `--break-at` is the **lowest threshold among the changed
files**, rounded down because Stryker takes a whole number. A file's threshold is the lower of two
numbers:

- **Its area's baseline**, from `test/Motiv.Tests/stryker-baselines.json`. The areas are the shards
  in `stryker-shards.json`, plus `rest`, assigned the same way `plan-shards.sh` assigns them.
- **Its own last measured score**, from `test/Motiv.Tests/stryker-file-baselines.json`, when it has
  one.

| Area | Baseline | Break |
|---|---|---|
| higher-order | 54.66% | 54 |
| expression-tree | 58.30% | 58 |
| decorator-result-predicate | 65.90% | 65 |
| predicate-traversal-shared | 71.60% | 71 |
| operators | 96.00% | 96 |
| rest | 77.59% | 77 |

Each area baseline is the area's score in the full run on `main` at `16b42fe` (2026-10-03) less one
point. It is the bar for a new file, and the most asked of an existing file that already scores
above it. The file baselines come from the same run.

Why both: the gate scores a handful of files, not the area. An area's score is an average, and
many files sit below it. In `operators`, which scores 97%, `NotSpecDescription.cs` scores 80%, so an
area floor alone would fail any change to that file, however good, for debt it already had. Holding
a weak file to its own score instead asks only that a change leave it no weaker. Stryker scores the
changed files together, and a pooled score is never below the lowest file's, so a pull request that
leaves every file at its measured score passes. The job summary lists the files held to their own
score.

What it does and does not promise:

- It is a **ratchet per file**, not a before/after comparison of the same file. Mutants a pull
  request adds count against the file's old score, so new code in a weak file must be at least as
  well tested as the file already was.
- The changed files are pooled, so a pull request that touches a weak file is held to that file's
  score as a whole: a strong file it also touches can slip without failing the gate.
- A file that scores 0% today puts no floor under a pull request that touches it. Bringing such a
  file up is the triage the full run is for.
- A new file is held to its area's baseline. In `operators` that means almost no survivors, and a
  small file gives a coarse score: three mutants with one survivor is 67%.
- The shared `stryker-config.json` keeps `break: 0`, so the full run stays report-only. The gate
  also raises `--threshold-low` to the break for its own run, because Stryker requires
  `break <= low`. That only changes the report's colours.

When a full run's scores rise, update both files in one pull request: raise the area's number in
`stryker-baselines.json`, keeping the one-point margin, and regenerate the file baselines from that
run's `src/Motiv` shard reports:

```bash
scripts/mutation/file-scores.sh <shard report>.json... > test/Motiv.Tests/stryker-file-baselines.json
```

Adding a shard to `stryker-shards.json` needs a baseline entry too: the gate fails and names any
area it touches that has no baseline.

### When there is no score

If the changed files produce no valid mutants (for example, every mutant is a compile error),
Stryker's score is NaN and it never breaks on NaN. The job summary then says **Gate not applied**,
so a green check is not mistaken for a pass. If Stryker exits 0 without writing a report, the job
fails.

### Reading the result

The job summary shows the score, the break and the areas behind it, and the files mutated. It also
lists each undetected mutant as `path:line:column  mutator  -> replacement`, and the same list is
printed to the log. The HTML and JSON reports are uploaded as the `mutation-report-motiv-pr`
artifact.

To reproduce a gate run locally, first work out the scope from the repository root. Then pass its
globs and break to Stryker from `test/Motiv.Tests`:

```bash
scripts/mutation/pr-scope.sh origin/main HEAD   # prints files, globs, areas, held files and break
cd test/Motiv.Tests
dotnet tool run dotnet-stryker --mutate "**/src/Motiv/Not/NotSpecDescription.cs" --break-at 80 --threshold-low 80
```

## Scheduled full run

Every Sunday at 03:17 UTC, and on any `workflow_dispatch`, the workflow mutates every project that
has a checked-in Stryker config, not just `src/Motiv`:

| Mutated | Tests run | Config | Shards |
|---|---|---|---|
| `src/Motiv` | `test/Motiv.Tests` | `stryker-config.json` | `stryker-shards.json` |
| `src/Motiv.Serialization` | `test/Motiv.Serialization.Tests` | `stryker-config.json` | `stryker-shards.json` |
| `src/Motiv.Analyzer` | `test/Motiv.Analyzer.Tests` | `stryker-config.json` | one `all` shard |
| `src/Motiv.CodeFix` | `test/Motiv.CodeFix.Tests` | `stryker-config.json` | one `all` shard |
| `@motiv-rules/core` | its Vitest suite (StrykerJS) | `pnpm -C ui/packages/rules-core mutate` | one job |

It runs weekly rather than nightly. `src/Motiv` alone takes about 150 runner-minutes, and
`src/Motiv.Serialization` is two-thirds its size. The code these runs measure changes over weeks,
and survivors are triaged by hand, so a nightly report would mostly repeat the previous one. To get
a fresh report sooner, dispatch the workflow.

A pull request that changes the mutation setup (the scripts, this workflow, the Stryker tool version,
or `test/Motiv.Tests`'s `stryker-config.json` or `stryker-shards.json`) runs the scripts' tests and
the `src/Motiv` shards alone. That proves the scripts, the `src/Motiv` config and
the workflow wiring. A change to another project's config, or to the PR gate's baselines, does not
start it, because this run would prove nothing about either: dispatch the workflow on your branch
to prove another project's config.

`scripts/mutation/plan.sh` builds the job matrix. A project with a `stryker-shards.json` is split the
same way as `src/Motiv`, including a `rest` shard. A project without one runs as a single `all`
shard. Every project's config keeps `thresholds.break: 0`, so the scheduled run reports and never
fails on a score.

`src/Motiv.Analyzer` and `src/Motiv.CodeFix` build `netstandard2.0` only, and their tests run on
`net10.0` only. Their configs therefore name no `target-framework`, because there is only one
framework on each side. `src/Motiv.Serialization` mutates `net10.0`, like `src/Motiv`.

Each job uploads a `mutation-report-<project>-<shard>` artifact. `@motiv-rules/core` uploads
`mutation-js-report-rules-core`, which holds `mutation.html` and `mutation.json`. It is named apart
from the `mutation-report-*` prefix because `combine` downloads the .NET shards' reports by that
pattern. Each job also prints its undetected mutants to its log and job summary, using the same
format as the `src/Motiv` shards. StrykerJS keys files relative to the package, so the workflow
passes `survivors.sh --prefix ui/packages/rules-core` to print repository-relative paths. `combine`
(`scripts/mutation/combine.sh`) writes one score per .NET project and names any shard that sent no
report. `@motiv-rules/core` writes its own score in its job summary.

`scripts/mutation/tests/scripts.test.sh` tests `plan.sh`, `survivors.sh`, `summarise.sh`,
`file-scores.sh` and `combine.sh` against fake reports in both tools' formats, and `pr-scope.sh` against a throwaway git
repository. It needs only `jq` and `git`, and the workflow runs it before planning.

## StrykerJS (@motiv-rules/core)

The TypeScript side runs [StrykerJS](https://stryker-mutator.io/docs/stryker-js/introduction/) with
the Vitest runner over `ui/packages/rules-core`. Its `src/expression/` is one half of the leaf
language whose contract with `Motiv.Serialization` is `test/expression/corpus.json`, so a mutant
that survives there is behaviour the corpus does not pin down. Report-only, like the .NET run.

### Running it

```bash
pnpm -C ui install                         # once
pnpm -C ui/packages/rules-core mutate      # stryker run, reads stryker.config.mjs
```

It mutates all of `src/**/*.ts` (about 4,800 mutants; roughly 11 minutes on 4 cores). To mutate a
subset, pass `--mutate`, which replaces the configured globs:

```bash
pnpm -C ui/packages/rules-core mutate --mutate "src/expression/**/*.ts"
```

The run copies the package into a sandbox, `ui/packages/rules-core/.stryker-tmp/sandbox-*/`, which is
git-ignored. The copy sits two directories deeper than the package, so a test that reaches outside
the package through a fixed relative path will miss its file there. `test/schema.test.ts` walks up
to `schemas/rule.v1.json` for this reason. Take care when adding a test like that. When the path was
fixed, `schema.test.ts` failed in its `beforeAll`, but the dry run was still reported as succeeded
and all of `src/schema.ts` showed up as NoCoverage. A whole file at 0% covered usually means this.

### Reports

- `ui/packages/rules-core/reports/mutation/mutation.html`: open this in a browser.
- `ui/packages/rules-core/reports/mutation/mutation.json`: the
  [mutation-testing-report-schema](https://github.com/stryker-mutator/mutation-testing-elements/tree/master/packages/report-schema)
  JSON, for tooling.

Both are git-ignored. The clear-text reporter also prints a per-file score table and every surviving
mutant to the console.

### What the checked-in config decides

`ui/packages/rules-core/stryker.config.mjs`:

- **`testRunner: 'vitest'`, `coverageAnalysis: 'perTest'`**: each mutant runs only the tests that
  cover it. The Vitest runner always works this way, and the config states it anyway.
- **`plugins`**: the runner is resolved from this package with `import.meta.resolve`. The default
  `@stryker-mutator/*` glob only looks next to `@stryker-mutator/core`. pnpm's isolated
  `node_modules` does not put the runner there, so the default fails with *"no TestRunner plugins
  were loaded"*.
- **`cleanTempDir: 'always'`**: the sandbox is deleted even after a failed run. A sandbox left
  behind holds a copy of `test/`, and `pnpm test` would collect those copies too.
- **`thresholds.break: null`**: report-only until the baseline has been triaged.
- **Version**: `@stryker-mutator/*` is pinned to 9.6.1. 10.x requires Node 22, and the `ui`
  workflows run Node 20.

### Marking an equivalent mutant

Use a comment on the line above, naming the mutator and giving a reason:

```ts
// Stryker disable next-line StringLiteral: the label is only read by a debugger
const label = 'scratch';
```

Syntax: `// Stryker [disable|restore] [next-line] <mutator list | all>[: reason]`. Without
`next-line`, a comment applies until a matching `restore`. The mutator name is shown in the
clear-text output (`[Survived] EqualityOperator`) and in the HTML report's drawer. A disabled
mutant is reported as `Ignored` and does not count towards the score. The same rule applies as for
the .NET run: prefer a test (for `src/expression/`, a corpus case) over a comment.
