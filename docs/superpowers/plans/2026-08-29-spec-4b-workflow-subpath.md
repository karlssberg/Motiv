# Spec 4B — The Workflow Subpath — Implementation Plan

**Design:** [2026-08-29-spec-4b-workflow-subpath-design.md](../specs/2026-08-29-spec-4b-workflow-subpath-design.md)
**Ticket:** [#148](https://github.com/karlssberg/Motiv/issues/148), the workflow half of ticket
[07](https://github.com/karlssberg/Motiv/issues/107)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 (the boundary) and §6 (build step 2)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169).
>
> **Ticket #148 carries the inventory** — both hand-rolled copies, what promotes and as what, and what
> stays app-side with the reason. It is not repeated here.
>
> **Paths are as of 4B**: the app was `ui/apps/demo`, rehomed to `ui/apps/studio` by Spec 4C.

## Global constraints

- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green. The
  supersession behaviours in particular are pinned with **hand-resolved deferreds**, since a race you
  cannot schedule is a race you cannot assert.
- **The demo's component suites are the oracle, and must pass *unchanged*.** That is the whole
  faithfulness argument for a promotion, and it constrains more than it looks: anything that would change
  observable page behaviour is out of scope *even when it is an improvement*, and gets filed instead.
- **The split is the requirement.** Workflow goes behind its own entry point, not a folder in the root
  barrel. Verified by the packaging, not by intent: tsup multi-entry, `exports` keys, and `external`.
- **`rules-core` stays framework-free**, enforced by pnpm strict resolution as in 4A.
- **Follow `DslSyncController`'s shape**, established in 4A: a framework-free class with
  `subscribe`/`getState`, coupled to the shared `RuleEditorStore`, adapted by a bindings-only hook.

## File structure

```
ui/packages/rules-core/src/workflow/index.ts             (new — the curated workflow barrel)
ui/packages/rules-core/src/workflow/ruleWorkflow.ts      (new — the rules save loop)
ui/packages/rules-core/src/workflow/propositionWorkflow.ts (new — the propositions loop)
ui/packages/rules-core/src/workflow/failureText.ts       (new — the text projections)
ui/packages/rules-core/{package.json,tsup.config.ts}     (the ./workflow entry point)
ui/packages/rules-core/test/workflow-{rule,proposition}.test.ts   (new — 50 controller tests)
ui/packages/rules-core/test/api-surface.test.ts          (the new surface pinned)
ui/packages/rules-react/src/workflow/{index,useRuleWorkflow,usePropositionWorkflow}.ts  (new)
ui/packages/rules-react/{package.json,tsup.config.ts}    (the mirrored entry point + external)
ui/packages/rules-react/test/api-surface.test.tsx        (new — the root pin core already had)
ui/apps/demo/src/panes/{PropositionsPage,RuleHeader}.tsx (shrink to rendering + route wiring)
ui/apps/demo/vite.config.ts                              (subpath aliases, *before* the roots)
ui/packages/rules-{core,react}/README.md                 (the second entry point, stated)
```

## Sequence

1. **The entry-point plumbing first**, before any logic moves: tsup multi-entry, the `exports` keys, the
   `external` addition, and the vite aliases — with the subpath entries ahead of the roots, since a
   plain-string alias also matches the ids that extend it. Getting this wrong later would look like a
   logic bug.
2. **`RuleWorkflowController`**, the smaller and better-understood loop: listing, load/unload (unload
   keeps the document — it is a local draft again), optimistic save sending `baseVersion` and adopting
   the returned version, the conflict record, and `invalid` routed into the shared store's error list.
3. **`PropositionWorkflowController`**: selection fetching document and blast radius together;
   save/remove/create; the supersession guard promoted out of `selectedRef` — as *two* mechanisms, an op
   token for `select` and a name comparison for outcomes; delete-vs-revert read off the entry's origin
   before the DELETE; revert refetching behind the surviving name.
4. **The pure projections** — `describePropositionFailure`, `describeUnexpectedFailure`, and both
   `whyNotSave` guards.
5. **The two barrels and their approved-API snapshots** (6 core symbols, 2 hooks), plus the root pin for
   `@motiv-rules/react` that `@motiv-rules/core` already had from 4A.
6. **The hooks**, on the `useDslSync` pattern: controller in state, `useSyncExternalStore`, a stable
   action surface memoised per controller, and the latest-ref indirection so the `onSelect` handover
   reaches the callback the component holds now.
7. **Shrink the pages** to rendering plus route wiring, and confirm their suites pass *unchanged*.
8. **The review pass**, then the full UI workspace: `pnpm -r build && pnpm -r typecheck && pnpm -r test`.

## The three review rounds

Recorded because they produced six real races rather than tidying; all six are argued in the design doc,
and each fix was pinned by a test watched failing first.

9. **Round 1 (the simplifier).** One `makeBinding` per hook — construction had been written twice per
   hook, with the `onSelect` ref-wiring duplicated verbatim, which is the one piece that must stay
   identical or a rebound controller silently loses the handover. Plus `remove()` re-checking the
   selection after the listing refresh.
10. **Round 2.** A `save()` resolving after a newer `load()` dragged the state back to the previous
    rule's identity or conflict; `refresh()` adopted a listing without re-deriving `loadedEntry`.
11. **Round 3.** Both `save()` methods issued a second PUT while one was in flight, and the earlier
    completion cleared `saving` under the one still running; `remove()`'s revert path handed over after
    an awaited reload without re-checking the selection.
12. **The asymmetry that was *not* fixed.** `RuleWorkflowController` rethrows where its sibling reports.
    Filed as [#150](https://github.com/karlssberg/Motiv/issues/150) rather than changed mid-promotion,
    because changing the error contract here would have broken the unchanged-suites argument. Spec 4J
    closed it.

## Not run

The Playwright e2e suite, which drives the .NET sample host — no .NET SDK was installable in that
environment ([#173](https://github.com/karlssberg/Motiv/issues/173)), and e2e is not in CI either. The UI
behaviour is pinned by the unchanged component suites instead.
