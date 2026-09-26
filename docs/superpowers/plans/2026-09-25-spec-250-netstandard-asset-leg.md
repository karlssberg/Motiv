# The netstandard2.0 asset leg — Plan

**Source spec:** issue #250.
**Design:** `docs/superpowers/specs/2026-09-25-spec-250-netstandard-asset-leg-design.md`

**Goal:** a `dotnet test Motiv.slnx` on macOS/Linux runs every test against the `netstandard2.0`
builds at least once, without giving up the `net472` Windows leg.

## Steps

1. **Red: the asset test.** Write `TargetAssetTests` in `Motiv.Tests` and
   `Motiv.Serialization.Tests`. It asserts that the loaded build's `TargetFrameworkAttribute` is
   `.NETStandard,Version=v2.0` under `MOTIV_NETSTANDARD_ASSET` or `NETFRAMEWORK`, and otherwise the
   test assembly's own framework. Add the `ns20asset` alias (`Directory.Build.props`) to both
   projects with no reference pin yet. Result: red, because the leg loads the `net10.0` builds.
2. **Green: pin the references.** Add a target in `Directory.Build.targets` that sets
   `SetTargetFramework` on every `ProjectReference`. It first failed for the transitive `Motiv` (see
   design decision 2). Fixed by hooking only `AfterTargets="IncludeTransitiveProjectReferences"`.
3. **Surface gates.** The leg exposed the seven `#if NET8_0_OR_GREATER` files in
   `Motiv.Serialization.Tests`, which reference `NET8`-only library types. Retarget them to
   `NET8_0_OR_GREATER && !MOTIV_NETSTANDARD_ASSET`.
4. **Red: resolvability.** 464 failures traced to a missing `Microsoft.Bcl.AsyncInterfaces`. Add the
   "every referenced assembly loads" theory and watch it name that assembly. Then reference the
   package for the leg only (`Directory.Build.targets`, version in `Directory.Packages.props`).
5. **Snapshots.** `Motiv.Serialization.Snapshots` ships `netstandard2.0` and its tests have no
   `net472` leg. Add the leg and the same `TargetAssetTests`. Red with the pin target disabled,
   green with it enabled.
6. **Verify.** Run a bare `dotnet build Motiv.slnx` (every TFM, `net472` included), then
   `dotnet test Motiv.slnx`.
7. **Docs.** Name the leg and the two rules it brings (surface gates; leg-only package references)
   in CLAUDE.md's ".NET side" section, and update the `dotnet-runtimes-user-local` memory note.

## Verification (2026-09-25, macOS, SDK 10.0.203)

| Suite | `ns20asset` | Other legs |
|---|---|---|
| `Motiv.Tests` | 6,044 passed | net8/9/10: 6,044 each |
| `Motiv.Serialization.Tests` | 1,265 passed | net8/9/10: 1,437 each (the difference is the NET8-only leaf tests) |
| `Motiv.Serialization.Snapshots.Tests` | 10 passed | net8/9/10: 10 each |
| Every other test project | n/a | green |

`net472` could not run locally (*Could not find 'mono' host*). It builds cleanly, and its binding
redirects cover every assembly the resolvability theory loads. Windows CI is the first place that
theory runs on `net472`.
