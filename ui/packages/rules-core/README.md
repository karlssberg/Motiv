# @motiv-rules/core

Headless, framework-free core for building Motiv rules-engine UIs. It owns the *logic* of
authoring — the rule-document model and its path arithmetic, the DSL (lexer, parser, printer,
spans), the subscribable editor store and its mutations, insertion planning, the accordion and
highlight view-state machines, node summaries, DSL/tree synchronisation, completion, and
diagnostics — and renders nothing.

## The boundary

- **No framework**: no React (that adapter is `@motiv-rules/react`; a Vue or Svelte adapter is
  bindings only, over the same `subscribe`/`getState` stores — and one has been written, so the
  size is a measurement rather than a guess).
- **No editor**: completion (`completeDsl`), diagnostics (`diagnosticsFor`) and token runs
  (`tokenSpans`) are expressed in this package's own neutral types. A CodeMirror integration
  maps them onto CodeMirror's shapes on its side of the boundary — this package takes no
  CodeMirror dependency, even at the type level. The exported word character classes
  (`WORD_START_CHARS` and friends) exist so integrations compose their regexes from the lexer's
  single definition instead of hand-copying it; the DSL vocabulary (`DSL_KEYWORDS`,
  `DSL_QUANTIFIERS`, `DSL_TYPES`) is exported for the same reason.

## The curated surface

The package root exports a chosen API, named export by named export — never `export *`. The
runtime surface is pinned by `test/api-surface.test.ts`; widening it is a deliberate edit to
that snapshot. Symbols a module exports but the root does not re-export are internal.

## The workflow entry point

`@motiv-rules/core/workflow` carries the authoring *session*'s logic — optimistic save with
version adoption, 409 conflict recovery, blast-radius reporting, and the failure-text
projections — as framework-free controllers (`RuleWorkflowController`,
`PropositionWorkflowController`) with the same `subscribe`/`getState` shape as the stores.
It is a separate entry point on purpose: taking the document logic from the package root never
drags in session opinions or the `RulesApiClient` coupling. Its surface is pinned by the same
approved-API snapshot.

## Runtimes

React is the supported adapter (`@motiv-rules/react`). Vue, Svelte and vanilla consumers bind the
`subscribe`/`getState` stores themselves, and what that costs is measured rather than estimated: a
worked Vue adapter offering the React surface symbol for symbol lives in `ui/examples/vue-adapter`
in this repository, is tested on every CI run, and is what the price table on the page below is
computed from. A .NET consumer, Blazor included, does not need this package at all: the same rule
documents are parsed, validated and evaluated by `Motiv.Serialization` in C#.

Framework-freeness is enforced rather than intended. This package declares no dependencies and no
peer dependencies, imports nothing outside itself, compiles with `DOM` removed from its TypeScript
`lib`, and is exercised by `scripts/isolated-consumer.mjs` — which packs it, extracts the tarball
into a tree where nothing else is installed, and drives both entry points through both the `import`
and `require` conditions while asserting that `react` does not resolve. That check runs in CI.

See [Runtimes and Support Tiers](https://github.com/karlssberg/Motiv/blob/main/docs/adoption/index.md).
