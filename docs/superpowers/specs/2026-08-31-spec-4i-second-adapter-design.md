# Spec 4I — The Second Adapter — Design

**Date:** 2026-08-31 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§6's last unbuilt item, under ticket [17](https://github.com/karlssberg/Motiv/issues/117). Tracked as
[#165](https://github.com/karlssberg/Motiv/issues/165); shipped as
[#166](https://github.com/karlssberg/Motiv/pull/166).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff, its follow-up commit, and the PR's review round. This is
> the eleventh and last of that backlog.
>
> **Ticket #165 states both defects, prescribes the shape, and predicts the interesting finding.**
> It is not repeated here.

## Summary

Ticket 17 offered a second adapter as *a credibility signal*. The slice declines that framing in its
first paragraph, and the reframing is the design.

Spec 4E's tier table sells the "enabled, not supported" tier on one sentence:

> the document bindings are **179 lines** … That is the real size of a Vue or Svelte adapter, because
> everything else … is already in the core and has no framework in it.

**Every clause after the "because" is checked by something** — `framework-free.test.ts` refuses a bare
import, the DOM-less TypeScript `lib` refuses the DOM, `isolated-consumer.mjs` drives the packed
tarball where `react` does not resolve. The clause *before* it — the number a buyer actually reads and
decides on — was arithmetic done on the React package and asserted to hold for a different framework.

So the adapter is not evidence that Motiv can do Vue. It is **the invoice, paid**, so that the price
list stops being an estimate.

## Decisions (locked)

### 1. `observe` is the whole adapter, and it is where the framework difference lives

Every composable in the package is a call to one 82-line primitive plus a handful of typed actions,
because everything an authoring UI needs is already computed in `@motiv-rules/core`. Three choices
inside it each answer something `@motiv-rules/react` does differently:

- **No snapshot caching**, and the reason is stronger than "Vue doesn't need it". The React adapter
  compares snapshot fields and returns the previous object when they match, because
  `useSyncExternalStore` tears without a referentially stable one. Here the comparison would be
  **wrong, not merely redundant**: the state a snapshot describes lives behind a new wrapper each
  time, so a binding that deduplicated on those fields would go *stale* rather than quiet.
- **`flush: 'sync'` on the rebind.** A source swap has to take effect before the consumer's next
  line, not on the next tick. React gets that free by re-running the hook during render.
- **`dispatch` rather than bound actions** — for the same reason, and it lives in `observe` once
  instead of in each composable.

### 2. Symbol for symbol, with exactly one exception, named

`api-surface.test.ts` pins the surface and then asserts it against the React adapter's list under a
single rename: `RuleEditorProvider` → `provideRuleEditorStore`, because React puts a value in context
with a component and Vue with a call inside `setup`. **The name differs and nothing else does.**

Writing the exception into the test, with its reason, is the decision. A test that quietly allowed
"close enough" would let the two columns of the price table stop comparing like with like — which is
the only thing that makes the comparison mean anything.

The surface pin also carries a second job here that it does not in the published packages: **the tier
table prices this surface**, so a symbol added without a decision would change the price without
changing the page.

### 3. `private: true`, and derived rather than listed

Motiv maintains **one** adapter. A second on the release train would say otherwise, so the package is
a workspace member that `pnpm -r build`/`typecheck`/`test` see and `pnpm -r publish` does not.

The pleasing part is that **Spec 4G's publish gate needed no edit at all.** It derives its publishable
set from `pnpm-workspace.yaml`'s globs minus everything marked `private`, precisely so a package added
later is gated by existing rather than by being remembered. Adding `examples/*` and one `private: true`
line was the entire integration, and `verify:publishable` still reports exactly the two packages.
That decision was made one slice earlier for a hypothetical; this is the hypothetical arriving.

### 4. Both tables are gated, not just the new one

`price.test.ts` measures the React source tree as well as the Vue one and fails when either marked
table drifts.

> half a comparison held to the source is the same defect as none of it

The React figures are what the Vue figures *mean anything against*. Gating only the new table would
leave the page's actual argument — that the two are within 5% — resting half on a measurement and half
on the same unchecked arithmetic the slice exists to replace.

Three smaller decisions inside that gate, each of which would be a defect the other way round:

- **The first part-predicate is deliberately the catch-all.** Every source file lands in exactly one
  row, so a file added *anywhere* moves a published number and the gate fires. A precise
  part-of-the-tree predicate could miss a file, and a missed file is priced at nothing.
- **The markers are read from the document**, so *which* table is being checked is a decision recorded
  in the page rather than a guess made in the test about which one came first.
- **The cell regex uses a lookahead on the closing pipe**, because two numeric cells are adjacent and
  consuming the delimiter between them would read only the first of every pair. Its test table carries
  prose with digits in it (`409 recovery`) to prove only whole-number cells are read.

### 5. The gate on imports is the mirror of the core's

`bindings-only.test.ts` reads the adapter's own source and refuses any bare import but `vue` and the
core. `@motiv-rules/core`'s `framework-free.test.ts` (Spec 4E) refuses *any* bare import at all.

Together they state the two halves of one sentence: **the core reaches for nothing, and the adapter
reaches only for the framework it adapts.** That sentence is the tier table's whole argument, and now
both halves are read off source rather than asserted.

### 6. The `JustificationTree` row is a correction, not an addition

This is the ticket's quieter defect and the one worth remembering. `JustificationTree` ships in the
React adapter alone. Both the adoption page and the accessibility page named it as *the* lone place
accessibility is inherited from a package — and **neither then said that a non-React adopter does not
inherit it.**

The tier table priced "your own bindings" and omitted the only accessibility the packages carry. A
price list that omits a line item is not incomplete; it is quoting for less than the goods. It is now
the third row of both tables: the markup costs 94 lines, and **the decision costs nothing, because it
is already written down** — which is the accessibility work of Specs 4D and 4H paying out to a runtime
they were never about.

## What paying the price found

| Part | React (lines / code) | Vue (lines / code) |
|---|---:|---:|
| document bindings | 179 / 127 | **250 / 140** |
| workflow entry point | 162 / 93 | **115 / 71** |
| `JustificationTree` | 98 / 59 | 94 / 59 |
| **total** | **439 / 279** | **459 / 270** |

Within 5%, so the page's headline claim survives. **The row it was quoting was the wrong one**, and it
was wrong in both directions:

- **The bindings cost more, not less**, and almost all of the difference is one file. React re-runs a
  hook every render, so the object an action closes over is always the current one, and swapping
  stores needs *no code at all*. Vue's `setup` runs once, so following a source that can change is an
  explicit `watch` and reaching the current controller an explicit indirection. **React's rebinding is
  not free, it is prepaid** — and it is the part of the price nobody had counted. Ticket #165 predicted
  exactly this under "expected fallout", which is the strongest thing that can be said for writing the
  prediction down before paying.
- **The workflow entry point costs less** — 115 against 162 — because reading the consumer's options
  *late* replaces a ref written from an effect, and with it the window in which an in-flight completion
  still reaches a superseded callback. The cheaper implementation is also the more correct one.

## The review round

Copilot raised three findings, **all real, and all in the two gates this slice adds** rather than in
the adapter. That distinction is the interesting part: a gate reporting a property it is not actually
checking is the one failure mode a gate cannot have, because everything downstream reads its green as
evidence.

| finding | what it would have let through |
|---|---|
| `bareImports` matched only `from '…'` | `import { createElement } from "react"` — a double quote, a dynamic `import()` or a `require()` all walked past the check that exists to forbid exactly that |
| `measure` counted `split('\n').length - 1` | one short for a file saved without a final newline — undercounting exactly the file an editor is most likely to produce, and only once committed |
| `published` took `indexOf('\n\n')` without checking `-1` | a marked table not followed by a blank line would `slice(0, -1)`, silently drop a character, and hand back numbers read from a misparsed document — a *passing* drift check on figures from somewhere else |

Two things are worth recording beyond the fixes:

- **The import gate had been written weaker than the thing it mirrors.** `@motiv-rules/core`'s
  `framework-free.test.ts` already covered all four spellings, in four separate patterns. The mirror
  covered one. Verified by putting both bypasses into `src/context.ts` and watching the gate fail on
  each once fixed.
- **Every published number is unchanged**, because every file in the repository ends with a newline
  today — *"that is the point at which the old code stopped being wrong by accident."* Same shape as
  Spec 4H's escaping fix, one slice earlier: a real defect whose output is byte-identical is exactly
  the defect nothing notices.

Eight tests now cover the gates' own reading: the import spellings and the quote character, a line
counted with and without its terminator, an absent marker, and a table that never ends.

## What this does not do

- **It does not publish a Vue package**, and must never. `CLAUDE.md` now carries that as a standing
  rule: the adapter stays off the release train and off `release-npm.yml`'s manifest list.
- **It does not make Vue a supported runtime.** The tier is still "enabled, not supported"; what
  changed is that its price is measured.
- **It does not run the axe sweep over it.** The adapter ships no application, and Studio is what the
  a11y gate scans. `JustificationTree`'s ARIA structure is covered by unit tests here — group names,
  `aria-controls` dropped rather than dangling when a group unmounts, and distinct ids for two trees
  on one page — which is the same set of behaviours Spec 4D established for the React one.
- **It does not close bundle spec 4.** Two obligations remain, neither addressable in a cloud
  container: the manual screen-reader pass (#172, a person's judgement) and the replacement hosted
  example for `src/examples/` (#171, needs a .NET SDK — #173).

## Verification obligations

All four gates were verified by breaking them, each restored afterwards:

| break | the gate that fired |
|---|---|
| a wrong React number in the page | `price.test.ts`, React table |
| a wrong Vue number in the page | `price.test.ts`, Vue table |
| a source file added to the adapter | `price.test.ts` (via the catch-all part predicate) |
| a `react` import in the adapter | `bindings-only.test.ts` |

The behaviour suites cover the store subscription — including that it leaves no listener behind and
follows a swapped store — the DSL sync controller's debounce, conflict and disconnect, both save
loops, the late read of the workflow options, and the explanation's ARIA structure.

## Outcome (recorded after the build)

Thirty-nine files, +1,648/−25. No C#, and nothing published changes.

- **1,192 UI tests green** — rules-core 684, **vue-adapter 53** (45 for the adapter, 8 added for the
  gates in the follow-up commit), rules-react 28, studio 427 — with typecheck clean across the
  workspace.
- `verify:publishable` still reports exactly `@motiv-rules/core` and `@motiv-rules/react`.
- The tier table's number stopped being an estimate, and gained a row it had been omitting.
- `CLAUDE.md` records the whole arrangement: the adapter is evidence, not a package; `pnpm -r publish`
  and `verify:publishable` skip it while `build`/`typecheck`/`test` do not; touching either adapter
  means editing `docs/adoption/index.md` in the same commit; and `bindings-only.test.ts` refuses any
  import beyond `vue` and the core.

### Where this sits in the series

This is the last slice of bundle spec 4's §6 machinery, and the last retrofit of the #169 docs
backlog. It also happens to be the slice that collects the most from the ones before it: Spec 4A's
curated barrel is what made a second adapter a *port* rather than a redesign; Spec 4E wrote the tier
table this slice audits and built the framework-free test it mirrors; Spec 4G's derive-don't-list
publish gate absorbed a new workspace member without an edit; and Specs 4D and 4H are why the
`JustificationTree` row could be ported for the cost of its markup alone.
