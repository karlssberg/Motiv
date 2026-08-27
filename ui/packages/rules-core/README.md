# @motiv-rules/core

Headless, framework-free core for building Motiv rules-engine UIs. It owns the *logic* of
authoring — the rule-document model and its path arithmetic, the DSL (lexer, parser, printer,
spans), the subscribable editor store and its mutations, insertion planning, the accordion and
highlight view-state machines, node summaries, DSL/tree synchronisation, completion, and
diagnostics — and renders nothing.

## The boundary

- **No framework**: no React (that adapter is `@motiv-rules/react`; Vue/Svelte adapters are
  ~200 bindings-only lines over the same `subscribe`/`getState` stores).
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
