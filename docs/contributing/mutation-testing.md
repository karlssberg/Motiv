# Mutation testing

Line coverage says which code the suite *executes*; mutation testing says which behaviour it
*checks*. [Stryker.NET](https://github.com/stryker-mutator/stryker-net) makes small changes
(mutants) to `src/Motiv` — flips a `&&` to `||`, empties a string, removes a statement — and runs
`test/Motiv.Tests` against each one. A mutant the suite still passes against has **survived**: some
behaviour changed and no test noticed. For Motiv that is most often explanation text — a test that
runs a path but asserts only `Satisfied` lets every mutant in `Reason`, `Assertions` or
`Justification` survive.

This is tracked in #294. Today the run is **report-only**: nothing fails on a low score.

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
- **`thresholds.break: 0`** — report-only until the baseline has been triaged.

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
any shard that sent no report.

To mutate one shard's files locally, pass the same globs, for example
`dotnet tool run dotnet-stryker --mutate "**/OrElse/**" --mutate "**/AndAlso/**"`.
