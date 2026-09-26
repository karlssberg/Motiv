# The netstandard2.0 asset leg — Design

**Date:** 2026-09-25
**Status:** Implemented
**Source spec:** issue #250, *Run the netstandard2.0 build of Motiv.Serialization under test on
non-Windows machines*. A `wayfinder:task` raised during expression leaves (#249), not a bundle spec,
so its body is the source spec.
**Plan:** `docs/superpowers/plans/2026-09-25-spec-250-netstandard-asset-leg.md`
**Unblocks:** #251 (bind expression leaves on netstandard2.0).

## Problem

`Motiv`, `Motiv.Serialization` and `Motiv.Serialization.Snapshots` ship a `netstandard2.0` build. A
`ProjectReference` resolves to the nearest compatible asset, so a `net8.0`/`net9.0`/`net10.0` test
leg loads the matching modern build and only `net472` picks `netstandard2.0`. `net472` runs on
`windows-latest` CI alone. A `netstandard2.0`-only break was therefore invisible until CI, and
`Motiv.Serialization.Snapshots.Tests` has no `net472` leg, so that package's `netstandard2.0` build
had never been loaded by a test anywhere.

## Decisions

1. **A target-framework alias, not a separate test project.** `ns20asset` is added to the
   `TargetFrameworks` of the three test projects whose library ships `netstandard2.0`. The alias
   declares its identity (`.NETCoreApp,Version=v10.0`) in `Directory.Build.props`, before the SDK's
   inference would try to parse the name. It keeps `dotnet test Motiv.slnx` one command, reuses
   every existing test file, and needs no second copy of each project's items. The ticket allowed
   either shape. A sibling project would have to mirror embedded resources, linked sources and
   package references and keep mirroring them.

2. **Every project reference is pinned, transitive ones included.** A target in
   `Directory.Build.targets` sets `SetTargetFramework="TargetFramework=netstandard2.0"` on every
   `ProjectReference` after `IncludeTransitiveProjectReferences`. A static item update would miss
   the transitive `Motiv` reference under `Motiv.Serialization.Tests`, and so would a target that
   also declared `BeforeTargets="AssignProjectConfiguration"`: the SDK's transitive-reference target
   hangs off that same hook, so ours ran first. Both produced a `netstandard2.0` serializer over a
   `net10.0` core, which is a combination no consumer can load. `TargetAssetTests` caught it.

3. **`MOTIV_NETSTANDARD_ASSET` separates surface from runtime.** Inside the leg, `NET8_0_OR_GREATER`
   is true because the runtime is .NET 10. The library's `#if NET8_0_OR_GREATER` APIs (the leaf
   language) are still absent, because it is the `netstandard2.0` build. Test gates that ask
   whether an API was compiled in become `NET8_0_OR_GREATER && !MOTIV_NETSTANDARD_ASSET`. Gates
   that ask about the runtime (`NETFRAMEWORK` around `GC.GetAllocatedBytesForCurrentThread`) keep
   their meaning. As a result, the `ExpressionsNotEnabled` test that used to run only on Windows
   now runs on every machine. When #251 lifts the gate, it deletes these `#if`s, and the corpus
   theories then run on this leg without any further change here.

4. **The one package the leg must name: `Microsoft.Bcl.AsyncInterfaces`.** NuGet restore does not
   read `SetTargetFramework`. It restores the leg as the `net10.0` it runs on, so packages that only
   the `netstandard2.0` builds depend on are never restored. Every one of those packages except
   this one is also a shared-framework assembly, so the runtime supplies it. On `net10.0`,
   `Microsoft.Bcl.AsyncInterfaces` resolves to its type-forwarding facade, which gives the same
   `IAsyncEnumerable` identity the runtime has. Before the reference was added, 464 of 1,263 tests
   failed with a `TypeInitializationException` in `MotivRulesTelemetry`.

5. **The guard names the gap.** Each test project gets a `TargetAssetTests`:
   - the loaded build's `TargetFrameworkAttribute` is `.NETStandard,Version=v2.0` on the leg (and
     on `net472`), and matches the test assembly's own framework everywhere else;
   - every assembly the loaded build references can be loaded.

   Both checks live in `test/testing/TargetAsset/TargetAsset.cs`, which each project links in as
   source, in the same way it links `StoreConformance`.

   The second test turns the next missing package into a single named assembly, instead of a cascade
   of type-initializer failures.

6. **`net472` stays.** The leg proves API shape and `#if` branches. It does not prove .NET Framework
   `Expression.Compile` quirks, and it does not prove package-supplied `System.Text.Json`
   behaviour. Windows CI keeps that job.

## Invariants

- `dotnet test Motiv.slnx` on macOS/Linux runs every test of `Motiv.Tests`,
  `Motiv.Serialization.Tests` and `Motiv.Serialization.Snapshots.Tests` against the
  `netstandard2.0` builds at least once.
- On `ns20asset`, no referenced Motiv assembly is a modern build.
- `Motiv.Analyzer`/`Motiv.CodeFix` are out of scope: they ship `netstandard2.0` only, so their
  tests already load that build.

## Known wrinkle

`dotnet test` labels the leg `(net10.0)`, reading it from the test assembly, so each opted-in project
prints two `(net10.0)` summaries. `-f ns20asset` selects the leg on its own.
