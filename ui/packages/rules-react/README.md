# @motiv-rules/react

Headless React adapter for `@motiv-rules/core` — bindings only. Every hook adapts one of the
core package's `subscribe`/`getState` stores or client calls to React's subscription
primitives; the authoring logic itself lives in `@motiv-rules/core`, and anything rendered is
delegated to the consumer.

- `RuleEditorProvider` / `useRuleEditorStore` — context for the shared `RuleEditorStore`.
- `useRuleEditor` — a component's subscription to the editor state.
- `useRuleNode` — one node's view over the store.
- `useCatalog`, `useEvaluation` — async client state.
- `useDslSync` — binds a `DslSyncController` (the DSL⇄tree sync machine in core) to the
  component lifecycle.
- `JustificationTree` — the one component: a render-prop projection of an evaluation's
  justification that owns the accessibility semantics and none of the markup.

`@motiv-rules/react/workflow` is the matching entry point for the session workflow —
`useRuleWorkflow` and `usePropositionWorkflow` bind `@motiv-rules/core/workflow`'s controllers
(optimistic save, 409 recovery, blast-radius reporting) to the component lifecycle, and nothing
more. It is split out for the same reason core splits it: the document bindings above are
takeable without the session workflow.

## Runtimes

This adapter is the supported React binding; it is not the only way to consume the core. See
[Runtimes and Support Tiers](https://github.com/karlssberg/Motiv/blob/main/docs/adoption/index.md)
for what a Vue or Svelte adapter costs — measured against this package, from a worked Vue adapter
that offers the surface above symbol for symbol — and for the .NET and Blazor path, which needs no
JavaScript package at all.
