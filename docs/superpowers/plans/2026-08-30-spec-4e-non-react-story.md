# Spec 4E — The Non-React Story — Implementation Plan

**Design:** [2026-08-30-spec-4e-non-react-story-design.md](../specs/2026-08-30-spec-4e-non-react-story-design.md)
**Ticket:** [#157](https://github.com/karlssberg/Motiv/issues/157), taking ticket
[17](https://github.com/karlssberg/Motiv/issues/117)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§3 (the two-runtime story), §6 (build step 5) and §7's first obligation

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #157 carries the grounded inventory —
> line counts, the existing cross-runtime schema test, the narrower .NET surface — and is not repeated
> here.

## Global constraints

- **TDD throughout**, with a twist particular to this slice: the properties being tested are already
  *true*. So a check is only worth writing if it can be made to **fail on purpose** — every guard here is
  validated by breaking the thing it guards, and that is part of the work rather than a nicety.
- **A guard that scans nothing passes loudly.** Anything pattern-based has to prove its own reach before
  its assertions mean anything.
- **The obligation is about the artefact, not the workspace.** Inside `ui/`, React is one `node_modules`
  away from every file, so no check that runs here can discharge "no React present". The proof has to
  leave the repository.
- **Documentation costs are cited, not estimated.** Every number in the support-tier table is read off
  the code it describes.
- **No C# is touched and no `dotnet test` is claimed.** The .NET tier is read off the public surface.

## File structure

```
ui/packages/rules-core/tsconfig.json               (lib: ES2022 — DOM dropped)
ui/packages/rules-core/test/framework-free.test.ts (new — the source guard, with its self-check)
ui/packages/rules-core/scripts/isolated-consumer.mjs  (new — pack, extract, drive, assert isolation)
ui/packages/rules-core/scripts/consumer/esm.mjs    (new — the ESM fixture)
ui/packages/rules-core/scripts/consumer/cjs.cjs    (new — the CJS fixture)
ui/packages/rules-core/package.json                (+ the verify script)
.github/workflows/ui.yml                           (+ the framework-free job)
docs/adoption/{index.md,toc.yml}                   (new — the four tiers, with measured costs)
docs/{toc.yml,Overview.md} ; README.md             (the pointers)
ui/packages/rules-{core,react}/README.md           (the page npm will show)
```

## Sequence

1. **Drop `DOM` from the core's `lib`** and confirm `src` and `test` still compile. This is the cheapest
   layer and it either works immediately or reveals a real dependency — either way it is one step.
2. **The source guard.** No bare specifier in `src/`, no `dependencies`/`peerDependencies` in the
   manifest, and every relative import `.js`-suffixed. Write the reach self-check *first*, so the
   assertions that follow are known to be scanning something. Bound the specifier pattern by `;`, not by
   newline — nearly every import here spans several lines.
3. **The isolated consumer.** `npm pack`, extract into a scratch tree under `tmpdir()` — **outside the
   repository**, or Node's upward `node_modules` walk finds the workspace's React and the assertion goes
   vacuous — then drive the store's subscribe/mutate/read contract, a DSL round trip, the projections and
   the `/workflow` entry point, through **both** conditions of the exports map, asserting `react`
   resolves nowhere.
4. **Break both halves to prove they work**: a bare React import in the core must fail the packed
   consumer; a planted `react/` in the scratch tree must fire the isolation assertion.
5. **Its own CI job**, so a regression reads as what it is rather than as a generic build failure.
6. **The support-tier table** in `docs/adoption/`, with the four tiers and their measured costs — and the
   .NET tier stated as narrowly as the public surface actually allows. Then the pointers: `docs/toc.yml`,
   `Overview.md`, the README section, and each package's own README, which is the page npm shows.

## The two review rounds

Argued in the design doc; recorded here because the second contains a refutation worth imitating.

7. **Round 1.** Both fixtures named *half* of `/workflow` each, so a build dropping either family would
   have passed the check claiming to validate it — each now names all six exports, and the way they do it
   differs because ESM fails at link time where CommonJS yields `undefined` and fails only on call.
   Separately, the catch block read `.message` off whatever was thrown and could report a failure as
   `undefined`.
8. **Round 2.** One finding taken (the README's .NET snippet defined neither `registry` nor its JSON, and
   called a JSON string `document` in a context where that word means something else). One **refuted**
   with two independent disproofs — `dirname` drops the trailing separator, and the CI job could not have
   passed on the previous head had the path been wrong — but the indirection that invited the misreading
   was removed anyway.

## Not run

No .NET suites, and none claimed: this slice changes no C#. The `.NET` tier of the table is read off the
public surface of `Motiv.Serialization`, and the cross-runtime schema invariant it depends on was already
discharged by the existing `schema.test.ts` / `RuleSchemaTests` pair.
