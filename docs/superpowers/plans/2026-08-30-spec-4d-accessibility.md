# Spec 4D — The Accessibility Slice — Implementation Plan

**Design:** [2026-08-30-spec-4d-accessibility-design.md](../specs/2026-08-30-spec-4d-accessibility-design.md)
**Ticket:** [#154](https://github.com/karlssberg/Motiv/issues/154), taking ticket
[18](https://github.com/karlssberg/Motiv/issues/118)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§4 (accessibility, enforced) and §6 (build step 4)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #154 carries the grounded inventory of
> what was already right and what was missing; it is not repeated here.

## Global constraints

- **TDD throughout**, with one wrinkle particular to this slice: axe findings are discovered, not
  predicted. The sweep is written and run *before* the fixes, and what it reports sets the work.
- **A role is a promise about behaviour.** Declaring one the surface does not implement is worse than
  declaring none — that rule decides the builder's markup and, later, licenses 4F's real `tree`.
- **Logic in the package, rendering in the app.** The accessible name is derived in `rules-core`;
  `a11y.ts` is string logic over the document model, so `rules-core` stays framework-free.
- **The gate must be deterministic and must not need .NET.** Both constraints point the same way: serve
  the built bundle, stub the API, and make the stub self-checking so an unstubbed call fails rather than
  scanning an error page.
- **Do not claim evidence that does not exist.** The manual half is authored, not performed, and the
  report says so per criterion.

## File structure

```
ui/packages/rules-core/src/a11y.ts               (new — accessibleExpression + the bound)
ui/packages/rules-core/test/a11y.test.ts         (new)
ui/packages/rules-core/src/index.ts              (+ the two new exports)
ui/packages/rules-core/test/api-surface.test.ts  (the surface pinned, as ever)
ui/packages/rules-react/src/JustificationTree.tsx   (flat tree → nested labelled groups)
ui/apps/studio/src/panes/BuilderPane.tsx         (the root group, named by the strip's one-liner)
ui/apps/studio/src/builder/RuleNodeEditor.tsx    (.node-kids → role="group"; the caret's aria-controls)
ui/apps/studio/src/builder/ListboxPicker.tsx     (the unconditional IDREF)
ui/apps/studio/src/shell/CommandPalette.tsx      (the "N of M" live region)
ui/apps/studio/src/panes/EvaluatePane.tsx        (the bare caret → a named disclosure)
ui/apps/studio/src/dsl/DslEditor.tsx             (the CodeMirror textbox's accessible name)
ui/apps/studio/src/styles/tokens.css             (--faint and the three pill colours)
ui/apps/studio/e2e-a11y/{axe.spec.ts,stubs.ts}   (new — the sweep and its self-checking fixture)
ui/apps/studio/playwright.a11y.config.ts         (new — its own config, its own command)
.github/workflows/ui.yml                         (the accessibility job)
docs/accessibility/{index.md,toc.yml}            (new — the report, the manual script, the honest gaps)
```

## Sequence

1. **`accessibleExpression` first**, in `rules-core`, with the 120-code-point bound and code-point-safe
   truncation. It is the thing everything else names groups with, and it is pure — so it can be fully
   tested before any markup moves.
2. **The accordion becomes nested labelled groups.** `.node-kids` gets `role="group"` named by its
   parent's expression; the caret gets `aria-controls`, **dropped while collapsed**. The builder root
   becomes a group described by the DSL strip's one-liner, covering the leaf rule with no subtree.
3. **`JustificationTree` gets the identical treatment**, replacing the flat run of sibling `treeitem`s.
   Rows carry the group id so `EvaluatePane`'s bare `▸` can become a named disclosure.
4. **The two defects found by reading**: `ListboxPicker`'s unconditional IDREF, and the palette's silent
   *"N of M"* count.
5. **The sweep, and the CI job.** `e2e-a11y/` with its own Playwright config and its own npm script;
   every view and every hard surface *in the state it is hard in*, both colour schemes, tagged
   `wcag2a`/`wcag2aa`/`wcag21a`/`wcag21aa`. Wire it as its own job in `ui.yml` — named for what it gates.
6. **Run it, then fix what it found.** Six violations: five contrast failures rooted in `--faint`
   (2.6:1 on white) and its three pill colours, and the DSL textbox with no accessible name. Re-quote
   every moved colour against the *tightest* ground it is actually drawn on, not the lightest.
7. **`docs/accessibility/`** — the conformance report, the manual screen-reader script, the two
   knowingly-approximate roles, and ticket 18 sub-5's statement that the SDK carries none.

## The two review rounds

Recorded because the second found a defect the first *introduced*; both are argued in the design doc.

8. **Round 1.** The bound's test asserted UTF-16 units against a limit stated in code points, and passed
   only because its fixture was ASCII — no astral coverage at all. Add two astral cases; **both pass
   against the code as it stands**, so this closes a hole in the tests. That coverage then licenses
   replacing `[...text]` with a two-step bound: the UTF-16 length as a cheap upper bound, then a walk
   that stops at the limit.
9. **Round 2.** The walk stops on `kept === max`, so a fractional or `NaN` limit never terminated it and
   the function returned the whole string — a guarantee the *previous* implementation had held by
   accident, through `slice`'s coercion. Normalise `limit` instead of trusting it. Separately,
   `JustificationTree` emitted `aria-label=""` for a node with no assertions or a blank label, which
   claims a name where there is none and reads differently in different readers; omit the attribute
   instead.

## Not run

The manual screen-reader pass — it needs NVDA or VoiceOver and a person, and is
[#172](https://github.com/karlssberg/Motiv/issues/172). The report states that per criterion rather than
implying an audit nobody ran.
