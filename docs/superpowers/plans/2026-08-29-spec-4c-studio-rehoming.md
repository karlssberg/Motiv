# Spec 4C — The Studio Rehoming — Implementation Plan

**Design:** [2026-08-29-spec-4c-studio-rehoming-design.md](../specs/2026-08-29-spec-4c-studio-rehoming-design.md)
**Ticket:** [#152](https://github.com/karlssberg/Motiv/issues/152), taking ticket
[08](https://github.com/karlssberg/Motiv/issues/108)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 (the app) and §6 (build step 3)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #152 carries the full rename table and
> the list of wiring around it; neither is repeated here.

## Global constraints

- **This slice moves code; it does not change it.** So its proof is the existing suites passing
  unchanged from their new home, not new tests. Anything that would change behaviour is out of scope
  *even when it is an improvement* — #150 was live at the time and was explicitly left for a later
  slice.
- **The compiler is most of the verification.** Every namespace, `ProjectReference`,
  `InternalsVisibleTo` and `Compile Include` in the rename is compiler-checked, so a green restore and
  test run is the real evidence. This matters because much of the change is invisible to a reader
  diffing 158 mostly-empty renames.
- **No .NET SDK in the authoring environment**, and the installer host blocked by egress policy. The
  .NET half is therefore verified in **CI**, not locally, and the PR must say so rather than implying a
  local run.
- **Rename by meaning, not by string.** A blanket find-and-replace on "sample" would rewrite accurate
  quality caveats into false claims — see the design doc's decision 2. Every site is classified before
  it is touched.

## File structure

The moves (ticket 08's table):

```
src/examples/Motiv.RulesEngine.Sample        → src/Motiv.Studio
src/examples/Motiv.RulesEngine.Sample.Tests  → src/Motiv.Studio.Tests   (SampleHost → StudioHost)
ui/apps/demo                                 → ui/apps/studio
@motiv-rules/demo                            → @motiv-rules/studio
```

The wiring that must follow them:

```
Motiv.slnx                  (out of the /Examples/ solution folder, in beside the other src/ projects)
.gitignore                  (seven runtime-artefact paths)
docker-compose.yml          (services demo/demo-auth → studio/studio-auth)
Dockerfile                  (+ its entrypoint assembly)
run-demo.sh → run-studio.sh ; Makefile: make demo → make studio
.claude/launch.json ; ui/apps/studio/README.md ; ui/pnpm-lock.yaml (the importer key)
ui/apps/studio/vite.config.ts        (outDir into the moved host)
ui/apps/studio/playwright.config.ts  (webServer command into the moved host)
docs/live-rules/AspNetCore.md        (new — "A complete host")
docs/{decision-log,governance,propositions,live-rules}/*.md   (four path references re-pointed)
```

## Sequence

1. **Classify the prose before moving anything.** Walk every occurrence of "sample" and sort it into
   *names the project* (renames) or *is a quality claim about `JsonFileRuleStore` /
   `JsonFilePropositionStore`* (stays). Doing this first is what keeps step 3 a mechanical rename rather
   than a judgement call made 17 times under pressure.
2. **Move the .NET projects** — host, then tests — with assembly and root namespace following, the three
   `ProjectReference` paths losing a directory level, the `StoreConformance` glob doing the same, and
   `SampleHost` becoming `StudioHost`. `InternalsVisibleTo` is `$(AssemblyName).Tests` and needs no edit.
3. **Move the SPA**: `ui/apps/demo` → `ui/apps/studio`, package renamed, `outDir` and `webServer`
   re-pointed, and `pnpm install` re-keying the lockfile importer.
4. **The wiring around them**, in one pass: solution file, `.gitignore`, compose, Dockerfile, run script,
   Makefile, launch config, README.
5. **Close the hosted-example gap in the docs** — "A complete host" in `docs/live-rules/AspNetCore.md`,
   plus the pointer to `src/Motiv.Studio` and its `Seam:` comments — and re-point the four `docs/**`
   references at the new path. Leave `docs/superpowers/**` alone.
6. **The stale-name sweep** as a gate: no `Motiv.RulesEngine.Sample`, `ui/apps/demo`,
   `@motiv-rules/demo`, `run-demo` or `SampleHost` outside `docs/superpowers/**`.
7. **UI workspace locally** (`pnpm -r build && -r typecheck && -r test`), **Playwright `--list`** to prove
   the moved `webServer` resolves, then **push and read CI** for the .NET half.

## Not run

- **The .NET suites, locally.** No SDK in the environment; verified green in CI on `25af8fe` instead —
  solution restore, full test run, and a Codecov patch report listing the moved files with no coverage
  change. `TreatWarningsAsErrors` being on repo-wide makes a green build the no-new-warnings check too.
- **The Playwright suite**, which starts the .NET host. Only `--list` was run, to prove the moved
  `webServer` command resolves.

This is the first slice in the series to hit that constraint head-on; it was later filed as
[#173](https://github.com/karlssberg/Motiv/issues/173).
