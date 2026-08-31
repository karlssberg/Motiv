# Spec 4I — The Second Adapter — Implementation Plan

**Design:** [2026-08-31-spec-4i-second-adapter-design.md](../specs/2026-08-31-spec-4i-second-adapter-design.md)
**Ticket:** [#165](https://github.com/karlssberg/Motiv/issues/165)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§6 — the last unbuilt item, a `rules-vue` adapter, offered by ticket
[17](https://github.com/karlssberg/Motiv/issues/117) as *"one second adapter as a credibility signal"*

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #165 states both defects, prescribes
> the private-workspace-member shape and the two gates, and predicts the interesting finding; it is
> not repeated here.

## Global constraints

- **The credibility signal is not the point.** Spec 4E's tier table sells a whole support tier on a
  number — *"the document bindings are 179 lines … that is the real size of a Vue or Svelte
  adapter"* — arrived at by measuring the React package and asserting it holds for a different
  framework. Every other clause in that sentence is checked by something; this one was arithmetic.
- **Write the adapter to find out, not to prove a point.** The measurement is allowed to contradict
  the estimate, and it does — in both directions.
- **Symbol for symbol with the React adapter**, or the two columns are not comparing like with like
  and the table means nothing.
- **`private: true`, permanently.** Motiv maintains one adapter; a second on the release train would
  say otherwise. It must be visible to `pnpm -r build`/`typecheck`/`test` and invisible to
  `pnpm -r publish` and Spec 4G's `verify:publishable`.
- **TDD throughout**, and the four new gates are themselves verified by breaking them.
- **No C#, and no published package changes.**

## File structure

```
ui/pnpm-workspace.yaml                          (`examples/*` — the member, and why it is private)
ui/examples/vue-adapter/package.json            (private, `vue` + core as deps)
ui/examples/vue-adapter/src/observe.ts          (82 lines — the whole adapter; see the design doc)
ui/examples/vue-adapter/src/{context,useRuleEditor,useRuleNode,useCatalog,useEvaluation,useDslSync,index}.ts
ui/examples/vue-adapter/src/workflow/{index,useRuleWorkflow,usePropositionWorkflow}.ts
ui/examples/vue-adapter/src/JustificationTree.ts (94 lines — the row the tier table was missing)
ui/examples/vue-adapter/test/price.test.ts       (155 lines — both tables, gated)
ui/examples/vue-adapter/test/bindings-only.test.ts (the mirror of the core's framework-free test)
ui/examples/vue-adapter/test/api-surface.test.ts   (symbol for symbol, with its one named exception)
ui/examples/vue-adapter/test/{mount,scope,sources}.ts + eight behaviour suites
docs/adoption/index.md                          (both marked tables + the second-adapter section)
docs/accessibility/index.md ; docs/Overview.md ; README.md ; CLAUDE.md
ui/packages/rules-{core,react}/README.md ; ui/pnpm-lock.yaml
```

Thirty-nine files, +1,648/−25.

## Sequence

1. **Make it a workspace member first.** `examples/*` in `pnpm-workspace.yaml`, `private: true` in the
   manifest. Confirm 4G's gate still reports exactly the two publishable packages — it derives its
   set from the globs minus `private`, so this should need no edit to the gate. It doesn't.
2. **Write `observe`**, the one primitive every composable is a call to. This is where the framework
   difference actually lives.
3. **Port the surface**, symbol for symbol, and pin it — with the one rename each framework's idiom
   forces, named and justified in the test rather than silently allowed.
4. **Port `JustificationTree`**, which is the row the tier table never had.
5. **Test against the same behaviours** the React adapter is tested against: store subscription
   (including leaving no listener behind and following a swapped store), the DSL sync controller's
   debounce/conflict/disconnect, both save loops, the late read of workflow options, and the
   explanation's ARIA structure.
6. **Measure both trees and publish the numbers** into marked tables in `docs/adoption/index.md`.
7. **Gate both tables** — the React one too, since half a comparison held to the source is the same
   defect as none of it.
8. **Gate the imports** — `bindings-only.test.ts`, the mirror of `@motiv-rules/core`'s
   `framework-free.test.ts`.
9. **Break all four gates** to confirm each fires: a wrong React number, a wrong Vue number, a source
   file added to the adapter, and a `react` import in it.
10. **Say the three things the estimate got wrong**, on the page, where the estimate was.

## The follow-up commit

Copilot found **three real defects in the two gates this PR adds** — not in the adapter. Each was the
gate reporting a property it was not actually checking, which is the one failure mode a gate cannot
have. All three fixed, with eight tests over the gates' own reading.

## Not run

The Playwright/axe gate and the .NET suite. Studio's sources are untouched (its 427 tests, including
Spec 4H's conformance drift gate, pass), and no C# is touched.
