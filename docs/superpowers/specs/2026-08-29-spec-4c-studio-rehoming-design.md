# Spec 4C — The Studio Rehoming — Design

**Date:** 2026-08-29 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Build step 3 of bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md)
§6, taking ticket [08](https://github.com/karlssberg/Motiv/issues/108)'s **evolve-in-place** resolution.
Tracked as [#152](https://github.com/karlssberg/Motiv/issues/152); shipped as
[#153](https://github.com/karlssberg/Motiv/pull/153).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog.
>
> **The rename table is ticket 08's and is reproduced in #152 and #153 in full.** It is not repeated
> here. This slice is 158 files of which most are pure renames; what is worth recording is the handful
> of places where a decision had to be made about *which* things move.

## Summary

Ticket 08 resolved evolve-in-place: the demo *is* the flagship, so it stops living in `src/examples/`.
`Motiv.RulesEngine.Sample` becomes `Motiv.Studio`, `ui/apps/demo` becomes `ui/apps/studio`, and
`@motiv-rules/demo` becomes `@motiv-rules/studio`, with the solution file, Docker compose, the
Dockerfile, the run script, the Makefile target and `.claude/launch.json` following.

Almost all of it is mechanical. Four decisions are not.

## Decisions (locked)

### 1. Why now, rather than at any other point in the series

Two things made this the moment rather than an arbitrary one, and both are consequences of the two
slices immediately before it.

**The boundary work is done.** Specs 4A and 4B moved roughly **1,700 lines** of authoring and workflow
logic into `@motiv-rules/core`. What remains in the app is rendering, the CodeMirror integration and
the host — which is *exactly* the shape ticket 08 assumed when it chose evolving the demo over building
a sibling app. Renaming before the promotion would have graduated a project that was still carrying the
library's logic.

**The name had become the only thing still saying "sample".** By this point the project had a
fail-closed dev identity (Spec 1), an EF-backed durable store (Spec 2C), a decision log (Specs 3B/3D)
and a telemetry surface (Spec 3C). Ticket 08's third sub-question — *"`src/examples/` is for examples. A
flagship app is not an example"* — had only sharpened.

### 2. "Sample" splits two ways, and only one of them renames

This is the slice's sharpest call. The C# carried two different words that both read as *sample*:

- **The project's name.** *"the sample's live rules"*, *"the sample's JwtBearer wiring"*, *"the sample
  creates its own schema"* — 17 sites across the C# and the docs. These become Studio.
- **A quality claim about two specific types.** *"it is a sample store, not a production one"*,
  *"acceptable for a sample"*, *"still a sample-grade answer"* on `JsonFileRuleStore` and
  `JsonFilePropositionStore`. **These stay exactly as they are.**

They stay because they remain *true* after the rename: the EF store from Spec 2C is the durable one, and
the JSON file stores remain the read-it-in-a-minute illustrations they always were. A blanket rename
would have converted an accurate caveat into **false advertising for the flagship** — the one outcome a
rename slice must not produce.

### 3. Two things held deliberately

**The Keycloak client id stays `motiv-demo`.** It is a registered client in the realm import
(`keycloak/motiv-realm.json`). Renaming it churns the realm, the `Motiv__Oidc__Audience` config and the
auth spec to change a string no reader of this repo learns anything from. The compose *services* rename
because they are this repo's own names for the app; **the IdP's name for its client is the IdP's.**

**`docs/superpowers/**` is not rewritten.** Those plans and designs are a dated archive of what was true
when they were written; back-dating paths into them would make them lie about their own history. docfx
already excludes them from the site.

> That second rule is why every retrofit in this backlog — including this document — states its paths
> as of its own slice and says so at the top, rather than silently using today's.

### 4. The hosted-example gap closes as documentation, not as a second project

Ticket 08 flagged the side effect in advance and prescribed the fix: graduating the project leaves
`src/examples/` with no hosted rules-engine example, while `docs/live-rules/AspNetCore.md` documents
those endpoints.

The answer is **A complete host** in that page — the smallest program that serves live rules, from the
spec registry through `app.Run()` — plus a pointer to `src/Motiv.Studio` as the worked example for
everything the snippet leaves out: store, identity, grants, gate, decision log. Each of those is marked
in the Studio source with a `Seam:` comment, so the pointer lands somewhere specific rather than at a
project.

Not a second host project. A second host is a second thing to keep building, and **ticket 08 had already
declined that trade on a solo-maintained repo.**

## What this does not do

- **It does not close spec 4 §7's Blazor obligation.** §7 owes *"the Blazor sample authors a valid rule
  document through `Motiv.Serialization` alone (no `rules-core`)"* — the load-bearing half of the
  two-runtime story Spec 4E later published as a support tier. Decision 4 closes the *documentation*
  half of ticket 08's gap; the demonstrable half is still owed, as
  [#171](https://github.com/karlssberg/Motiv/issues/171) (Spec 4K), which remains open.
- **It does not split the backend.** Ticket 08: 285 lines, one host, the question dissolves.
- **It does not change library behaviour.** `Motiv`, `Motiv.Serialization` and the npm packages are
  untouched. The breaking surface is paths and names for anyone running the app from a checkout —
  `./run-studio.sh`, `make studio`. `docker compose up` is unchanged, since the renamed default service
  is still the only one without a profile.
- **It does not fix [#150](https://github.com/karlssberg/Motiv/issues/150)**, the rule controller's
  missing failure channel — that changes behaviour rather than moving it, and is Spec 4J.

## Verification obligations

- The solution restores, builds and tests with both projects at their new paths. This is the rename's
  only real proof: every namespace, `ProjectReference`, `InternalsVisibleTo` and `Compile Include` in it
  is compiler-checked.
- `pnpm -r build`, `-r typecheck` and `-r test` pass with the app at `ui/apps/studio` under its new
  package name. The workspace glob is `apps/*`, so the rename must carry the **lockfile importer** with
  it.
- The Vite `outDir` and the Playwright `webServer` command both resolve into the moved host.
- **A stale-name sweep**: no occurrence of `Motiv.RulesEngine.Sample`, `ui/apps/demo`,
  `@motiv-rules/demo`, `run-demo` or `SampleHost` survives anywhere outside `docs/superpowers/**`.

## Outcome (recorded after the build)

Shipped as [#153](https://github.com/karlssberg/Motiv/pull/153): **158 files, +209 / −139** — the ratio
that says "rename". `InternalsVisibleTo` is `$(AssemblyName).Tests`, so it followed for free.

**1,003 UI tests green locally** — `rules-core` 608, `rules-react` 22, `apps/studio` 373 — with the Vite
build writing into `src/Motiv.Studio/wwwroot`, confirming the moved `outDir`, and `pnpm install`
re-keying the lockfile importer from `apps/demo` to `apps/studio`. Playwright's `--list` enumerated 36
tests in 10 files against the moved `webServer` command; the suite itself needs the .NET host and was
not run.

**The .NET half was verified in CI rather than locally** — no .NET SDK in the authoring environment, and
the installer host blocked by its egress policy. CI came back green on `25af8fe`: solution restore, full
test run, and a Codecov patch report listing the moved files at `src/Motiv.Studio/*.cs` with no coverage
change. `TreatWarningsAsErrors` is on repo-wide, so a green build is also the no-new-warnings check.

> This is the **first slice in the series to state that constraint explicitly**, and to lean on CI as the
> compiler of record for a change it could not compile locally. It recurs through every later slice and
> was eventually filed as [#173](https://github.com/karlssberg/Motiv/issues/173) — which is also what
> makes #171, the one remaining piece of ticket 08's gap, unbuildable by an unattended cloud session.
