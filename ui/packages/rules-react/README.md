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
