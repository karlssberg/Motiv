# Spec 4B — The Workflow Subpath — Design

**Date:** 2026-08-29 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Build step 2 of bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md)
§6 — the **workflow half** of ticket [07](https://github.com/karlssberg/Motiv/issues/107)'s boundary,
following Spec 4A's domain half. Tracked as [#148](https://github.com/karlssberg/Motiv/issues/148);
shipped as [#149](https://github.com/karlssberg/Motiv/pull/149).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff and its **three** review rounds.
>
> **Ticket #148, like #146 before it, is unusually complete** — it inventories both hand-rolled copies,
> names what promotes and as what, and lists what stays app-side. That inventory is not repeated here.
>
> **Paths are as of 4B**: the app was `ui/apps/demo`, rehomed to `ui/apps/studio` by Spec 4C.

## Summary

Ticket 07 scoped the boundary as *"domain **and** workflow …, split across **separate entry points** so
an adopter can take document logic without inheriting session opinions."* Spec 4A took the domain half.
This takes the workflow: roughly **260 lines** of optimistic save, 409 recovery and blast-radius
reporting, hand-rolled twice in the demo and **drifted in capability** — the rules loop never grew the
failure banner, the stale-selection guard, or the thrown-error reporting the propositions loop had.

Two framework-free controllers move behind `@motiv-rules/core/workflow`, with bindings-only hooks behind
`@motiv-rules/react/workflow`. The demo pages shrink to rendering plus route wiring, and their existing
component suites pass **unchanged** — which is the promotion's proof of faithfulness, and which turns out
to be the reason one asymmetry was deliberately left in place.

## Decisions (locked)

### 1. A separate entry point, not a folder inside the root barrel

The split is the requirement, not an implementation detail. Importing `parse` / `print` / the editor
store from the package root must never drag in save orchestration **or its `RulesApiClient` coupling** —
an adopter authoring documents offline should not acquire a transitive dependency on a session's HTTP
client.

So it is a real entry point on both sides: tsup multi-entry (`src/index.ts`, `src/workflow/index.ts`)
plus a `./workflow` key in each package's `exports` map, and `@motiv-rules/react`'s tsup config adds
`@motiv-rules/core/workflow` to `external` alongside the root.

Each new surface gets its own **approved-API snapshot**, extending 4A's rule that the published surface
is a deliberate list rather than an `export *`: six core symbols, two hooks. `@motiv-rules/react`'s root
also gains the runtime pin `@motiv-rules/core` got in 4A, closing the gap between the two packages.

### 2. The alias order in `vite.config.ts` is load-bearing

The demo aliases the packages to their `src/` during tests, because the `exports` fields point at a
gitignored `dist/`. Adding a subpath meant discovering that **a plain-string Vite alias also matches the
ids that extend it**, appending the remainder to the replacement: the root alias alone resolves
`@motiv-rules/core/workflow` to `src/index.ts/workflow`.

The subpath entries are therefore listed **before** the roots, and the config says so at the alias. This
is recorded because the failure is silent-ish and the fix is invisible to anyone reading the alias list
top-down without knowing the matching rule.

### 3. Supersession promotes from a ref into the model — as two mechanisms, not one

`PropositionsPage` guarded async continuations with a `selectedRef`: an outcome lands only while the
selection it was aimed at is still on screen. Promoting that rule split it in two, because the page had
been using one ref for two different jobs:

- **`select` is op-token-guarded** — a superseded load never lands, because a newer `select` has already
  bumped the token. The question is *"is this still the newest load?"*
- **Save and delete outcomes compare against the controller's current selection by name**, exactly as the
  ref did. The question is *"is this still aimed at what is on screen?"*

The rules loop gains the same discipline for its listing refresh and loads — the capability its copy had
never grown.

### 4. Navigation is reported, never performed

The page owns routing and continues to. The controller hands the selection over through an `onSelect`
callback — a created proposition's name, a reverted name, `null` after a delete — and the route drives
`select()` back in.

That keeps the controller framework-free and router-agnostic, but it puts a real burden on the React
binding, which decision 8's review round then found had been half-met:

- The `onSelect` handover is kept pointed at the **latest** callback via a ref updated every render. The
  controller is built once per `(client, store)`, while the consumer's `onSelect` is typically an inline
  closure with a new identity every render; the handover must reach the one the component holds *now*,
  not a stale closure captured at construction.
- The action surface is **memoised per controller**, not per snapshot, so a `select`-on-route-change
  effect does not re-fire every time the state it changes comes back around.

### 5. Delete-versus-revert is read before the DELETE, because the response cannot say

Removing a proposition means one of two things: deleting an authored one, or reverting an overridden one
to its compiled spec. The API's response does not distinguish them, so the discrimination is read off the
**entry's origin before the request is issued**. A revert then refetches behind the surviving name — the
proposition still exists, at version 0, served by its compiled spec.

### 6. Failure text and save availability are pure projections

`describePropositionFailure` renders every typed refusal — `conflict`, `nameTaken`, `referenced`, and
`invalid` with its `brokenDependents` — and `describeUnexpectedFailure` covers what the API cannot model.
Both `whyNotSave` guards promote as pure functions, including the propositions page's version-0 *"served
by a compiled spec"* case.

The precedent is `nodeSummary`'s, established before this bundle: **text, not rendering.** The package
answers *why* Save is unavailable; the disabled control is the consumer's rendering of that answer.

### 7. One asymmetry is deliberately kept, and filed instead of fixed

The review pass flagged that `RuleWorkflowController` **rethrows** where `PropositionWorkflowController`
**reports** into a failure channel. That is a genuine wart — and it was left alone.

The reason is decision-shaped rather than lazy: this slice's proof of faithfulness is *the demo's
component suites passing unchanged*. The rules loop rethrows because `RuleHeader` rethrew; changing the
error contract mid-promotion would have made the suites disagree with the pages for a reason unrelated to
the move, dissolving the only evidence the promotion was behaviour-preserving.

So it was filed as [#150](https://github.com/karlssberg/Motiv/issues/150) and fixed later, on its own, by
**Spec 4J** — where the argument for changing it is that both loops now sit side by side in a *published*
surface with opposite error contracts.

## Three review rounds, six races

The rounds found more than tidying, and every finding was a real race, so they are design rather than
cleanup. Each fix was pinned by a test watched failing first.

**Round 1 — the simplifier pass.**
- Each hook built its controller **twice**, in the `useState` initializer and again in the rebind branch,
  with `usePropositionWorkflow`'s `onSelect` ref-wiring duplicated verbatim. That wiring is precisely the
  piece that must stay identical, or a **rebound controller silently loses the handover**. Both hooks now
  build through a single local `makeBinding`.
- `remove()` re-checks the selection after the listing refresh, closing an inherited race where a
  delete's navigation handover could land after the user had moved on.

**Round 2 — two races the rule controller inherited, and its sibling had already been cured of.**
- A `save()` resolving after a newer `load()` dragged the state back to the previously saved rule's
  identity or conflict. `save()` now captures the load generation and drops a superseded outcome.
- `refresh()` adopted a listing **without re-deriving `loadedEntry`**, so a rule loaded before the
  listing arrived kept a stale `null` while the rules moved on.

**Round 3 — three more, all real.**
- **Both** `save()` methods issued a second PUT when called while one was in flight, and the earlier
  completion then cleared `saving` **under the one still running** — so `whyRuleSaveUnavailable` /
  `whyPropositionSaveUnavailable` would report a save was available mid-flight. The guard the flag
  already *described* is now enforced by the controllers rather than merely advertised to consumers.
- In `remove()`'s revert path the handover followed an awaited reload without re-checking the selection,
  so a user who moved on during that refetch was dragged back to the reverted entry — decision 3's rule,
  now applied across the last `await` too.

The pattern across all three rounds is one rule applied unevenly: **every `await` in a flow that can be
superseded needs the check, not just the first one.**

## What this does not do

- **It does not unify the two error contracts.** Decision 7; #150, then Spec 4J.
- **It does not promote `AdminPage`'s grant editing.** Its one 409 is the last-administer guard — a
  different surface, not this loop.
- **It does not promote rendering.** The AppBar/Toolbar/palette, both banners, `DependentsStrip`, the
  proposition dialog and its seeds, and the route itself stay app-side, per ticket 07.
- **It does not make the packages publishable.** The `exports` maps added here use a single `types` key
  per entry. Because both packages are `"type": "module"`, that later proved to make them unimportable
  from CommonJS; **Spec 4G** split declarations per condition (`.d.ts` under `import`, `.d.cts` under
  `require`) as part of the first publish. Nothing was published between the two, so the defect never
  reached an adopter.

## Verification obligations

- The controller suites run **framework-free** in `rules-core`, with no React present — enforced by pnpm
  strict resolution, as in 4A.
- The supersession behaviours are pinned as controller tests: a stale save or delete outcome never lands
  on a newer selection, and a superseded refresh, load or select never overwrites a newer one.
- The demo's existing `RuleHeader` (14) and `PropositionsPage` (42) component suites pass **unchanged**
  against the promoted controllers — same behaviour, new home.
- Both new entry points export exactly their approved lists.
- A second `save()` while one is in flight does not issue a second PUT.

## Outcome (recorded after the build)

Shipped as [#149](https://github.com/karlssberg/Motiv/pull/149): 22 files, **+1712 / −285**, in four
commits — the promotion and the three rounds.

**The pages shrank to rendering plus route wiring.** `PropositionsPage.tsx` 400 → 177 lines;
`RuleHeader.tsx` 175 → 122.

**Tests: 930 → 995.** `@motiv-rules/core` 542 → 600, `@motiv-rules/react` 15 → 22, demo 373 unchanged —
and *unchanged* is the number that matters, since it is the faithfulness argument.

**Not run:** the Playwright e2e suite, which needs the .NET sample host; no .NET SDK was installable in
that environment ([#173](https://github.com/karlssberg/Motiv/issues/173)), and e2e is not in CI either.
Flagged honestly rather than claimed; the UI behaviour is pinned by the unchanged component suites.

**The `code-simplifier` pass ran as a general review agent** — no such agent existed in that environment,
as for #141–#147.
