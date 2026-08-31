# Spec 4E — The Non-React Story — Design

**Date:** 2026-08-30 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Build step 5 of bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md)
§3 and §6, taking ticket [17](https://github.com/karlssberg/Motiv/issues/117), and discharging §7's
first obligation. Tracked as [#157](https://github.com/karlssberg/Motiv/issues/157); shipped as
[#158](https://github.com/karlssberg/Motiv/pull/158).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff and its two review rounds.
>
> **Ticket #157 carries the grounded inventory** — the line counts, the existing schema test, the
> narrower .NET surface. It is not repeated here.

## Summary

Ticket 17's answer is a **two-runtime story**: React is the supported JS adapter, other JS frameworks
are cheap DIY over a *verified-neutral* core, and .NET goes through `Motiv.Serialization` directly. Its
sub-4 says the deliverable is the honest table, because *"the illegitimate thing is leaving it
undocumented."* Spec 4 §7 makes the other half mechanical: **`rules-core` builds and its tests pass with
no React present — enforced, not asserted.**

Neither half held. The word *React* appeared **nowhere in the README**, and the core's neutrality rested
on nobody having yet written the wrong import.

## Decisions (locked)

### 1. Three layers of enforcement, because each catches what the others cannot

Framework-freeness is not one property, and no single check covers it:

| Layer | What it catches | Why the others miss it |
|---|---|---|
| **The compiler** — `lib: ["ES2022"]`, DOM dropped | `document`, `window`, `localStorage` in a package that renders nothing | Not an import, so no import guard sees it |
| **The source guard** — `framework-free.test.ts` | Any bare specifier in `src/`; any `dependencies`/`peerDependencies` | Fails *at the point the invariant breaks*, not two steps downstream |
| **The packed consumer** — `isolated-consumer.mjs` | Everything the first two are blind to: the `files` field, the `exports` map, and whether the artefact actually works | Inside the workspace, React is one `node_modules` away from every file |

That last row is the whole argument for the third layer. **The property is only observable outside the
workspace.** An accidental React import in the core would resolve, typecheck and pass its tests here —
so a check that runs here proves nothing about it.

Dropping DOM cost nothing: `src` and `test` already compiled without it, and `fetch` survives because
`@types/node` declares it.

### 2. The scratch tree lives outside the repository

`isolated-consumer.mjs` packs the package with `npm pack`, extracts the tarball into a scratch tree
where **nothing else is installed**, and drives it from plain Node.

The scratch tree is created under `tmpdir()`, deliberately outside the repository: **Node resolves
`node_modules` by walking up**, so a scratch tree inside `ui/` would find the workspace's React and the
central assertion would be vacuous — passing exactly as loudly as a correct one.

`npm pack` rather than `pnpm pack`, because npm ships with Node and therefore runs wherever the check
does.

### 3. Type-only imports count as dependencies

`import type { … } from 'react'` erases at build time, so it is tempting to exempt. It is not exempted:
it still obliges the consumer to have React's *types* installed, which is a dependency by any honest
reading. The source guard treats it like any other bare specifier.

### 4. The specifier pattern is bounded by the statement terminator, and the test checks itself

Nearly every import in this package spans several lines, so a line-bound `[^;\n]*?` would **scan almost
nothing while appearing to scan everything** — the worst failure mode available to a guard. The pattern
is bounded by `;` instead, which still stops the match running on into the code below an import.

And because a pattern matching nothing would pass every assertion below it in silence, the suite asserts
its own reach first: more than ten modules, and **more specifiers found than there are modules**.

### 5. Both halves were checked by breaking them

The check is only worth its CI minutes if it fails when it should:

- A bare React import in the core → **fails the packed consumer.**
- A `react/` planted in the scratch tree → **fires the isolation assertion**, rather than passing
  silently.

### 6. The .NET tier is stated more narrowly than ticket 17 phrased it

`RuleDocument`, `RuleNode` and `RuleDocumentParser` are `internal`. The public authoring surface of
`Motiv.Serialization` is `Validate`/`Deserialize` over **JSON text**, plus `SpecRegistry`, the stores and
the governance types.

So the honest claim is: a .NET consumer **validates, binds and evaluates** rule documents with
`Motiv.Serialization` alone, and **composes them as JSON** — because there is no C# equivalent of the
TypeScript mutations, path arithmetic or DSL printer. Spec 4 §7's Blazor obligation holds in exactly
that sense, and the page says so **until a C# authoring API exists** — which is a new public surface and
a decision, not something to smuggle in under a docs slice.

### 7. No Vue adapter, and the check is the credibility signal instead

Ticket 17 makes a second adapter optional and conditional on resourcing, naming the *neutral core* as
the deliverable. The isolated-consumer check is the credibility signal a second adapter would have been
— **without a second published package to keep green forever.**

> Spec 4I later shipped a Vue adapter anyway, and resolved this tension rather than reversing it: it is
> `private: true`, so it is **evidence, not a package**. It never joins the release train, and its job is
> to make the price this table publishes *checkable* — `test/price.test.ts` measures both adapters and
> fails when they drift from the marked tables on this page.

### 8. Every cost is measured, and the table says which number is which

`@motiv-rules/react` is **439 lines**: **179** of document bindings (what a Vue or Svelte adapter would
rewrite), **162** behind the `/workflow` entry point (taken only if wanted), and **98** in
`JustificationTree`, the one component.

Ticket 17's *"~200 bindings-only lines"* is the **first** of those three numbers, and the table says so
rather than quoting the total or the estimate.

## Two review rounds

**Round 1 — three findings, all confirmed.**

- **Both fixtures named half of `/workflow` each**: the ESM one asserted the rules save loop, the CJS one
  a single proposition export. The subpath carries **two independent families** plus the failure-text
  projections, so a build that dropped either family would have passed the check that claimed to
  validate it. Each fixture now names all six exports.
- **And the way they name them differs, because the module systems differ.** Under ESM a missing named
  export is a **link-time `SyntaxError` before the file runs**; CommonJS resolves it to `undefined` and
  only fails when something *calls* it — which is exactly the condition a partial build would hide in.
  Verified by dropping one export from the workflow barrel and rebuilding: ESM fails to link, and the
  CJS loop names the missing export, each on its own.
- The catch block read `.message` off whatever was thrown. A string, or a child-process failure shape
  without one, would have reported the failure as `isolated-consumer: undefined`.

**Round 2 — one taken, one refuted.**

- **Taken:** the README's .NET snippet used `registry` and `document` without defining either, so it read
  as a fragment rather than something to follow — and `document` named *a string of JSON* in a context
  where "document" is also the name of the thing that string encodes. It now defines both, and the JSON
  is called `json`, here and in `docs/adoption`.
- **Refuted:** `packageDir` was said to resolve to `scripts/`, breaking `npm pack` and the fixture paths.
  It does not — `dirname` drops the trailing separator — and **the CI job that runs this script had
  passed on the previous head**, which it could not have done had the path been wrong. Two independent
  disproofs. *But* two layers of indirection to express "the directory above this one" is what invited
  the misreading, so it now says that directly: `fileURLToPath(new URL('../', import.meta.url))`.

> The refutation is worth keeping as a pattern: a bot finding is a bug report, so it is **verified**
> rather than accepted or dismissed — and when it turns out to be wrong, the code can still be the
> thing that changes, because the confusion it caused was real.

## What this does not do

- **No Vue adapter** — decision 7, later revisited by Spec 4I on different terms.
- **No Blazor sample project.** Ticket 08 declined a second UI to maintain on a solo repo, and Spec 4C
  closed the equivalent gap in the docs for the same reason. What remains of that obligation is
  [#171](https://github.com/karlssberg/Motiv/issues/171).
- **No public C# document model or DSL.** Decision 6: named as a follow-up rather than smuggled in.
- **No C# changes at all, and no `dotnet test` claimed.** The slice is TypeScript and documentation; the
  .NET half of the table is read off the public surface rather than exercised.
- **Not the npm publish** (ticket 22) — Spec 4G. The `exports` map this check first exercises honestly is
  also where 4G found the CommonJS declarations defect.

## Verification obligations

- `rules-core` builds and its tests pass **with no React present** — as the packed tarball, in a tree
  where `react` does not resolve, over **both module formats and both entry points**.
- The core is DOM-free **by construction**, not by inspection.
- No module in `src/` names anything outside the package — type-only imports included — and the manifest
  declares no dependencies of any kind.
- The guard proves its own reach before asserting anything.
- Every cost in the support-tier table is cited from the code it describes.

## Outcome (recorded after the build)

Shipped as [#158](https://github.com/karlssberg/Motiv/pull/158): 14 files, **+565 / −2**, in three
commits — the slice and the two rounds.

**1,099 UI tests green** (`rules-core` 684, `rules-react` 28, `studio` 387), build and typecheck clean,
and the isolated consumer green over both module formats and both entry points.

**The two properties this slice makes mechanical were both already true** — the runtime closure was
empty and the package was DOM-clean. Nothing was fixed; what changed is that neither is any longer *"an
accident of good behaviour"*. That is the whole point of the slice, and the reason its diff is almost
entirely new checks and prose.
