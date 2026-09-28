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

`--break-at` is the **lowest post-triage baseline among the areas the changed files sit in**. The
areas are the shards in `stryker-shards.json`, plus `rest`, assigned the same way `plan-shards.sh`
assigns them. The value is rounded down, because Stryker takes a whole number. The baselines live in
`test/Motiv.Tests/stryker-baselines.json`:

| Area | Baseline | Break |
|---|---|---|
| higher-order | 39.85% | 39 |
| expression-tree | 57.08% | 57 |
| decorator-result-predicate | 45.33% | 45 |
| predicate-traversal-shared | 68.98% | 68 |
| operators | 72.46% | 72 |
| rest | 75.70% | 75 |

Why a threshold per area rather than one number: the gate scores a handful of files, and areas
differ by more than 35 points. One global number set to the overall score (~56%) would fail every
change to `higher-order` for debt that is already there. Set to the lowest area, it would let an
`operators` file fall to 40%. When a pull request spans several areas, their mutants are scored
together against the lowest of their baselines, so the gate stays lenient across areas.

What it does and does not promise:

- It is a **floor per area**, not a per-file before/after comparison. A file that already scores
  below its area's baseline fails the gate the first time it is touched, until tests bring it up.
  That is the intended ratchet, but it can land on a small, unrelated fix.
- A small file gives a coarse score. Three mutants with one survivor is 67%.
- The shared `stryker-config.json` keeps `break: 0`, so the full run stays report-only. The gate
  also raises `--threshold-low` to the break for its own run, because Stryker requires
  `break <= low`. That only changes the report's colours.

When the full run's area score rises, raise that area's number in `stryker-baselines.json` in the
same pull request. Adding a shard to `stryker-shards.json` needs a baseline entry too: the gate
fails and names any area it touches that has no baseline.

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
scripts/mutation/pr-scope.sh origin/main HEAD   # prints files, globs, areas and break
cd test/Motiv.Tests
dotnet tool run dotnet-stryker --mutate "**/src/Motiv/Not/NotPolicy.cs" --break-at 72 --threshold-low 72
```
