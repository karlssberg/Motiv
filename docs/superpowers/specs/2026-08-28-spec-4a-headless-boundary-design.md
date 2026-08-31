# Spec 4A — The Headless Boundary — Design

**Date:** 2026-08-28 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Build step 1 of bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md)
§6 — the domain half of ticket [07](https://github.com/karlssberg/Motiv/issues/107) (the SDK/app
boundary), behind the curation gate ticket [06](https://github.com/karlssberg/Motiv/issues/106) puts in
front of it. Tracked as [#146](https://github.com/karlssberg/Motiv/issues/146); shipped as
[#147](https://github.com/karlssberg/Motiv/pull/147). First slice of bundle 4.

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. The decisions below are recovered from the shipped diff and its review round.
>
> **Ticket #146 is unusually complete** — it names the modules to move, what each becomes, and what
> stays app-side, module by module. Where a decision is already argued there, this document cites it
> rather than restating it, and concentrates on the four things the ticket did not settle: how the
> barrel is pinned, what the neutral shapes actually are, the sync machine's lifecycle, and the five
> defects the review round found.
>
> **Paths in this document are as of 4A.** The app was `ui/apps/demo`; Spec 4C later rehomed it to
> `ui/apps/studio`.

## Summary

`@motiv-rules/core` exported **106 symbols** via `export *`, of which the demo and `rules-react`
together imported about **30**. Every module was re-exported wholesale — including the printer's inline
helpers and the lexer's character-class fragments, which are exported *to the barrel* only because they
are exported *to a sibling module*.

This slice curates that barrel down to a chosen surface, then promotes the authoring logic that was
still sitting in the app: the accordion and highlight state machines, node mutations, node summaries,
child paths, token runs, the DSL vocabulary, completion, diagnostics, and the DSL⇄tree sync machine.
`RuleTree` is removed. The result is a package that owns the logic of authoring and renders nothing.

## Decisions (locked)

### 1. Curation comes first, and the order is the whole point

Ticket 06 gates ticket 07: *"curate `rules-core`'s barrel **before** ticket 07's promotion — `export *`
publishes 100 unchosen symbols today and would **auto-publish everything promoted**."*

That sequencing is not tidiness. Promoting first would have taken every internal helper of every
promoted module and published it, silently, in the same commit that moved it. The barrel is curated
first so promotion lands *inside* a boundary that already exists.

### 2. The barrel is explicit; the snapshot pins values, and the barrel itself pins types

The root is named export by named export — **never `export *`**. `parseGeneration` and the `dsl/index`
sub-barrel stop being public.

`test/api-surface.test.ts` pins the result: **69 approved runtime values**, alphabetically, asserted as
an exact set. Widening or narrowing the published surface is a deliberate edit to that list, never a
side effect of an `export` keyword somewhere in the package.

The division of labour is worth stating, because it looks like a gap and is not. The snapshot pins
*values* only. **Types are enforced by the explicit barrel itself** — a type that is not re-exported
does not resolve at a consumer, so the compiler is already the test. Adding a runtime-value assertion
for types would duplicate a check TypeScript performs more reliably.

### 3. Neutral shapes, and the string-smuggling they end

Ticket 07 requires the packages to declare their own completion/diagnostic/token-run types and take
**no CodeMirror dependency, even at the type level**. `CompletionItem`, `DslCompletion` and
`RuleDiagnostic` are those declarations; the demo keeps thin adapters that map them onto CodeMirror's
`Completion`/`CompletionResult`/`Diagnostic`.

`RuleDiagnostic` is where this stops being bookkeeping. The old lint produced CodeMirror `Diagnostic`s
and **smuggled the machine-readable code through the message string**, joined with a `": "` separator
that `hover.ts` then had to split back apart. Two modules shared an undeclared encoding through a field
typed as human-readable prose. Promoting it as `{ from, to, severity, code, message, path? }` makes the
code a field; joining it into one display string becomes the CodeMirror adapter's private business, on
its own side of the boundary.

`path` is optional for a real reason: a backend error is keyed by node path and mapped through the
parse's spans, while a parser error carries native offsets and **has no node yet**.

### 4. One definition of the vocabulary and the character classes, because a copy had already drifted

`DSL_KEYWORDS` / `DSL_QUANTIFIERS` / `DSL_TYPES` existed twice — as the lexer's private classification
sets, and as the demo's exported constants in `motivLanguage.ts`. The lexer's own comment already told
the matching story about the word-character classes: a hand-copied class drifted, so once dots were
admitted to spec words in the lexer, the copy kept stopping at the dot and **completion past a namespace
dot returned nothing**.

So the lexer holds the single definition and exports it — vocabulary and `WORD_START_CHARS` /
`WORD_REST_CHARS` / `PARAM_REST_CHARS` alike — and integrations compose their regexes from it rather
than hand-copying. `completion.ts` builds `WORD_BEFORE_CURSOR` that way.

The classes are not interchangeable, which is why all three are exported: a parameter reference uses the
**non-dotted** `PARAM_REST_CHARS` because parameter names are not namespaced, while a spec word uses the
dotted `WORD_REST_CHARS` because a spec name may be.

### 5. The sync machine promotes framework-free, with following the store made explicit

`useDslSync.ts` was a React hook in the app holding a debounce-parse-commit loop, a self-commit guard,
and the conflict rule. The machine is not React's, so it becomes `DslSyncController` in core with the
same `subscribe`/`getState` shape as `RuleEditorStore`, and `@motiv-rules/react` gains `useDslSync` as a
**bindings-only** wrapper — which is where a React binding belongs, rather than in the app.

The one addition the promotion required is `connect()`. Nothing is reconciled until it is called, and
disconnecting cancels any in-flight commit. A hook can subscribe on mount and unsubscribe on unmount
because the machine makes that a first-class operation instead of an implicit consequence of
construction.

### 6. `RuleTree` is removed, not deprecated

Ticket 07 names it *"now inconsistent with the boundary"*; spec 4 says removed rather than deprecated.
Nothing was ever published, so removal costs nothing — and its only remaining consumer was **its own
test**. `JustificationTree` stays, per [#99](https://github.com/karlssberg/Motiv/issues/99): it is the
render-prop component that owns the accessibility semantics and none of the markup, which is the one
shape a headless package can legitimately ship.

### 7. Mutations deduplicate against the document model rather than beside it

`builder/mutations.ts` moved as-is in the ticket's plan, but landed deduplicated: the higher-order
handling folds into the document model's own `higherOrderKey` / `higherOrderBody` rather than
re-deriving them. A promoted module that re-implements what the package it is joining already knows is a
copy waiting to drift — the same failure mode as decision 4, caught before it could start.

## The review round (five findings, four of them real defects)

The `code-simplifier` pass produced more than tidying, so it is recorded as design rather than as
cleanup:

- **The self-commit guard now resets in a `finally`.** A foreign store subscriber throwing mid-commit
  could latch it, after which *every* later external change would be silently adopted as the
  controller's own — sync broken for good, with nothing announcing it. The next change now reconciles
  as a conflict, which is the honest outcome.
- **A disconnected controller no longer commits to the shared store.** `setText` after disconnect keeps
  the buffer (dirty) but schedules nothing; reconnect plus re-arm picks it back up. A controller a
  binding has released should not still be writing.
- **`useDslSync` holds its controller in `state`, not `useMemo`.** React documents the memo cache as
  discardable, and discarding it here would evaporate the user's **uncommitted buffer**. A store swap
  rebinds during render, per the documented adjust-state-on-prop-change pattern.
- **`literalCountOf` is exported**, so the count a control displays and the count the mutations commit
  share one fallback rule. `QuantifierNode` now uses it. Two independent fallbacks for the same number
  is decision 4's failure mode in miniature.
- **A tautological test became behavioural.** `N_QUANTIFIER_KINDS` was asserted against itself; it now
  rekinds to every kind and checks that `n` is attached exactly when the kind carries a count. The
  completion fixture likewise stopped being a cast — which had been hiding a phantom field — and is
  compiler-checked.

## What this does not do

- **The workflow subpath.** Optimistic save, 409 recovery and blast-radius reporting stay app-side, with
  `RuleHeader` and `PropositionsPage` each carrying a hand-rolled copy of the save/conflict loop — which
  is the argument for promoting them. Ticket 07 wants workflow behind its **own entry point** so
  document logic does not drag session opinions along; that is build step 2 (Spec 4B, #149).
- **The CodeMirror integration and the shell.** `motivLanguage.ts`, `payloadChips.ts`, `hover.ts`,
  `DslEditor.tsx`, the popover/anchoring hooks and everything in `shell/` stay app-side — ticket 07
  excluded them by name.
- **The rehoming to `Motiv.Studio`** (ticket 08) and the replacement hosted example — Specs 4C and 4K.
- **The npm publish itself** (ticket 22), which *follows* this curation and promotion — Spec 4G.
- **axe, the manual pass and the VPAT** (ticket 18) — Specs 4D, 4H and 4L.

## Verification obligations

- `rules-core` builds and its tests pass with **no React present**: the promoted modules and their tests
  are `.ts`, and the package keeps zero React dependencies, so framework-freeness is enforced rather
  than asserted (spec 4 §7's first obligation).
- The root exports exactly the approved list, asserted as an exact set rather than a subset.
- The package imports no CodeMirror type, transitively included.
- The demo compiles and its suites pass against the promoted modules — same behaviour, new home. This is
  the promotion's proof of faithfulness.
- A completion offered past a namespace dot returns options, which is what the drifted copy could not do.
- A store change under a dirty buffer raises a conflict rather than clobbering either version; a
  subscriber that throws mid-commit does not latch the guard.

## Outcome (recorded after the build)

Shipped as [#147](https://github.com/karlssberg/Motiv/pull/147): 49 files, **+1565 / −761**, in two
commits — the promotion, then the review round.

**106 exported symbols → 69 approved runtime values**, against roughly 30 that consumers actually
imported. The curated surface is deliberately wider than current usage: it includes the character
classes and vocabulary that decision 4 requires integrations to compose from.

**Net deletion from the app.** `builder/childPaths.ts`, `builder/mutations.ts` and `dsl/useDslSync.ts`
(and its 194-line test) leave `ui/apps/demo` entirely; `dsl/completion.ts` and `dsl/lint.ts` shrink to
adapters.

**`rules-react`'s surface stayed the same size** — eight runtime exports, one swapped: `RuleTree` out,
`useDslSync` in. The adapter gained a binding and lost a component, which is the boundary's shape in one
line.
