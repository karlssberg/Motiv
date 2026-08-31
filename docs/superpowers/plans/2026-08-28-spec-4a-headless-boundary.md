# Spec 4A — The Headless Boundary — Implementation Plan

**Design:** [2026-08-28-spec-4a-headless-boundary-design.md](../specs/2026-08-28-spec-4a-headless-boundary-design.md)
**Ticket:** [#146](https://github.com/karlssberg/Motiv/issues/146), taking ticket
[07](https://github.com/karlssberg/Motiv/issues/107)'s domain half behind ticket
[06](https://github.com/karlssberg/Motiv/issues/106)'s curation gate
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 (the boundary) and §6 (build step 1)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169).
>
> **Ticket #146 already carries the module-by-module inventory** — what moves, what each becomes, and
> what stays app-side with the reason. It is not repeated here; this is the order the work went in and
> the constraints that governed it.
>
> **Paths are as of 4A**: the app was `ui/apps/demo`, rehomed to `ui/apps/studio` by Spec 4C.

## Global constraints

- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green.
- **Curate before you promote.** `export *` would auto-publish every internal of every promoted module,
  in the same commit that moved it. The barrel is narrowed first so promotion lands inside a boundary
  that already exists. This orders the whole slice.
- **Promotion is behaviour-preserving.** The demo's suites are the oracle: same behaviour, new home. A
  module that changes while it moves cannot be checked by the tests that already cover it, so anything
  that must change (the neutral shapes) changes *deliberately* and brings its own tests.
- **No CodeMirror, even at the type level.** Not a dependency, not a `import type`. The adapters live on
  the app's side of the boundary.
- **No React in `rules-core`.** The promoted modules and their tests are `.ts`; the package keeps zero
  React dependencies. This is spec 4 §7's first verification obligation and it is enforced structurally,
  not asserted.

## File structure

```
ui/packages/rules-core/src/index.ts              (the curated barrel — named exports, never `export *`)
ui/packages/rules-core/test/api-surface.test.ts  (new — the approved-API snapshot)
ui/packages/rules-core/src/{accordion,highlight,nodeSummary,mutations,paths}.ts   (promoted)
ui/packages/rules-core/src/dsl/{completion,diagnostics,tokenRuns}.ts             (promoted, neutral shapes)
ui/packages/rules-core/src/dsl/lexer.ts          (the single definition of vocabulary + char classes)
ui/packages/rules-core/src/dslSync.ts            (new — DslSyncController, with connect())
ui/packages/rules-core/test/*.test.ts            (the promoted suites, plus five new ones)
ui/packages/rules-react/src/useDslSync.ts        (new — bindings only)
ui/packages/rules-react/src/RuleTree.tsx         (deleted, with its test)
ui/apps/demo/src/dsl/{completion,lint}.ts        (shrink to CodeMirror adapters)
ui/apps/demo/src/{builder/childPaths,builder/mutations,dsl/useDslSync}.ts        (deleted)
ui/packages/rules-{core,react}/README.md         (the boundary, stated)
```

## Sequence

1. **Measure, then curate.** 106 exported symbols against ~30 imported. Write the explicit barrel and
   the approved-API snapshot first, so every later step's additions are a visible diff to that list.
   `parseGeneration` and the `dsl/index` sub-barrel drop out here.
2. **The as-is moves**, which the ticket already settled: accordion, highlight, node summaries, child
   paths, token runs. Their existing tests move with them and must pass unchanged — that is what makes
   these steps cheap and worth doing first.
3. **The vocabulary and character classes**, exported from the lexer as the single definition. The demo's
   duplicate constants in `motivLanguage.ts` derive from it. Rebuild completion's word regex from the
   exported classes, and cover the case the drifted copy could not do: completion past a namespace dot.
4. **Completion and diagnostics under neutral shapes.** `CompletionItem`/`DslCompletion` and
   `RuleDiagnostic`, with the demo keeping thin adapters. The diagnostic's `code` becomes a field, so the
   `": "` join `hover.ts` had been splitting back apart becomes the adapter's private business.
5. **Mutations**, deduplicated against the document model's own `higherOrderKey`/`higherOrderBody` rather
   than re-deriving them.
6. **`DslSyncController`.** The debounce-parse-commit loop, the self-commit guard and the conflict rule,
   framework-free, with `connect()` as the explicit lifecycle. Then `useDslSync` in `rules-react` as a
   bindings-only wrapper, and the app's 194-line hook test retires in favour of tests against the machine.
7. **Remove `RuleTree`** and its test. `JustificationTree` stays per #99.
8. **The two READMEs**, stating the boundary — what the core owns, what the adapter is allowed to be, and
   that the root surface is curated and pinned.
9. **The `code-simplifier` pass**, then the full UI suite.

## The review round

Recorded because it produced four real defects rather than tidying, and they are argued in the design doc:

10. **The self-commit guard resets in a `finally`** — a throwing store subscriber could otherwise latch
    it and make every later external change adopt silently as the controller's own.
11. **A disconnected controller stops committing** to the shared store.
12. **`useDslSync` holds its controller in `state`, not `useMemo`** — a discarded memo cache would
    evaporate the user's uncommitted buffer.
13. **`literalCountOf` is exported**, so the displayed count and the committed count share one fallback.
14. **Two tests stopped being vacuous**: the tautological `N_QUANTIFIER_KINDS` assertion became
    behavioural, and the completion fixture became compiler-checked instead of cast — which had been
    hiding a phantom field.

## Not run

Nothing .NET-side is touched, and at this point the UI suite is the whole story.
