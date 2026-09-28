# PROTOTYPE: motiv-fix runner (throwaway)

This answers karlssberg/Motiv#297: can a runner that ships with the Motiv agent skill apply the `MOTIV0001` code fix to one chosen expression, choosing the code action by equivalence key? This branch is never merged; `main` keeps only the decisions it produced.

**Verdict: yes.** A .NET 10 file-based app with about 140 lines does it, using `MSBuildWorkspace`, the analyzer run in-process, and `RegisterCodeFixesAsync` for the one chosen diagnostic.

## Run it

Copy `consumer/` out of the repository first. Otherwise this repo's `Directory.Build.props` applies to it. Then:

```bash
dotnet build src/Motiv.CodeFix -c Release
dotnet pack src/Motiv -c Release -o <feed>   # point consumer/nuget.config at <feed>, fix the version in Consumer.csproj
A=src/Motiv.CodeFix/bin/Release/netstandard2.0
dotnet run motiv-fix.cs -- list --project <consumer>/Consumer.csproj --analyzers $A
dotnet run motiv-fix.cs -- fix  --project <consumer>/Consumer.csproj --analyzers $A \
    --file <consumer>/Domain/Order.cs --line 12 --action ConvertToSpec [--column N] [--dry-run]
```

## Findings

1. **The Motiv package does not ship the analyzer or the code fix.** `nupkg` contains only `lib/*/Motiv.dll`. The `ProjectReference … OutputItemType="Analyzer"` items are not packed, and both projects are `IsPackable=false`. A consumer build reports no `MOTIV0001`. The runner therefore loads `Motiv.Analyzer.dll` and `Motiv.CodeFix.dll` itself (`--analyzers`) and runs the analyzer in-process, so the project needs no analyzer reference. It also means a SARIF build log cannot be the discovery step; the runner's `list` command replaces it.
2. **Both actions can be chosen.** `ConvertToSpec` and `ConvertToSpecWithDebugOutput` are both reachable by equivalence key. An unknown key returns an error that lists the available keys.
3. **Line numbers shift after a fix.** Converting line 12 moved the remaining diagnostics down by four lines (19 → 23, 24 → 28). The agent must run `list` again after every fix, or a batch mode must apply a file's fixes from the bottom up.
4. **Several expressions can share a line.** `var x = a > 1; var y = b > 2;` gives two diagnostics on one line. Without `--column` the runner refuses and prints the columns.
5. **`MOTIV0001` fires broadly.** It flagged all six top-level comparisons: a null guard, a `for` bound and an infrastructure retry check, as well as three domain rules. Choosing locations by context is essential.
6. **The fixer does not merge duplicates.** The two identical `order.Total > 100 && … == "GB"` conditions are converted into separate propositions. Merging them is the agent's job.
7. **Bug in `Motiv.CodeFix`, not in the runner:** converting the `return` in a block body with an earlier statement (`if (order is null) throw …;`) **dropped that statement**. The result still compiles.
8. **Timing (M-series Mac, SDK 10.0.203):** the first `dotnet run` takes about 8.7 s wall-clock, including compiling the runner. Warm runs take about 6 s wall-clock, of which about 1.4 s is opening the project, about 1–2 s is analysis and about 0.3 s is the fix. A batch mode, with one workspace load for many fixes, is worth building.
9. **Trim warnings:** file-based apps default to `PublishAot=true`, which floods the build with trim warnings about reflection loading. `#:property PublishAot=false` silences them.
