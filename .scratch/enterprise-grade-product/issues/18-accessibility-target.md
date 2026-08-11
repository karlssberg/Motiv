# The accessibility target, and what enforces it

Type: grilling
Status: resolved
Blocked by: 08

## Question

WCAG 2.1 AA is in scope. For a product sold into enterprises — and especially into the public sector —
accessibility is a procurement gate, not a polish item. But the surface being made accessible depends
on ticket 08's answer, which is why this waits.

The session must resolve:

1. **What is the target, precisely?** WCAG 2.1 AA is the stated floor. Is a VPAT / Accessibility
   Conformance Report an output of this effort, or out of scope? (Procurement asks for the document,
   not the compliance.)
2. **Which surfaces?** The authoring UI certainly. The command palette, the modal document viewer,
   and the CodeMirror DSL strip are the hard cases — CodeMirror has its own accessibility story that
   must be inherited rather than invented, and a custom command palette is a well-known trap for
   focus management and screen-reader announcement.
3. **The accordion builder is the real problem.** A nested, expandable tree of rule nodes with inline
   editing, insertion points, popover cards, and toolbars is one of the harder things to make
   keyboard-navigable and screen-reader-coherent. Is it a `tree` role, a set of nested
   `disclosure`s, or something else? Does a screen-reader user understand the *composition* — which
   is the entire point of the UI?
4. **What enforces it?** `axe-core` in the existing Playwright suite is the cheap mechanical answer
   and catches perhaps half of AA. The other half — focus order, announcement quality, meaningful
   labels on generated content — needs manual audit. Is manual audit in scope, and who does it?
5. **Does the SDK carry any of this?** If ticket 07 promotes components into packages, accessibility
   becomes a *package* property that every consumer inherits — a strong selling point. If packages
   stay headless, accessibility is entirely the app's, and consumers get no help.
6. **Justification output is generated content.** `JustificationTree` renders a hierarchy of
   assertions from arbitrary adopter-supplied strings. Its accessible rendering is a package concern
   regardless of where the boundary lands — it is one of only two components `rules-react` ships.

## Inherited from ticket 07 — sub-question 5 is answered, unfavourably

**The packages ship no components, so accessibility is entirely the app's.** Consumers get no help
from the SDK, and the "accessibility as an inherited package property" selling point in sub-question 5
is off the table under this boundary.

One exception survives and is worth weighing: `JustificationTree` renders a hierarchy of
adopter-supplied assertion strings, and is one of only two components `rules-react` exports today.
Whether it survives the boundary at all is ticket 06's call — `RuleTree`, its sibling, is already
flagged for reconsideration because its only consumer rejected it.

## Inherited from ticket 08

Sub-question 2's surfaces are settled: the app is the **evolved demo**, renamed `Motiv.Studio` at
`ui/apps/studio`. So the audit targets the existing 6,819 lines — the accordion builder, the
CodeMirror DSL strip, the command palette and the modal viewer — not a hypothetical new UI. Every
hard case named in this ticket is a real file today, which makes the scope knowable rather than
speculative.

## Grounded

- **`axe-core` is not wired up.** The only "axe" hit in the UI is an unrelated comment in
  `highlight.ts`. The "cheap mechanical half" of AA enforcement is a **gap to close**, not a thing to
  ratify.
- The hard-case components (`RuleNodeEditor`, `OperatorPicker`, `CommandPalette`, `Modal`, …) are real
  files with tests. `JustificationTree` is one of two components `rules-react` ships.

## Answer

**WCAG 2.1 AA is the floor and a VPAT is the procurement deliverable. The accordion builder is *not* a
`role=tree` — it is nested labeled groups plus disclosure, and Motiv's own generated
`Reason`/`Justification` text is the authoritative accessible description of the composition. Enforcement
is `axe-core` in Playwright for the mechanical half plus a required manual screen-reader pass for the
rest.**

### The key move — explainability *is* the accessibility affordance (sub-3)

The accordion builder — nested expandable rule nodes with inline editing, insertion points, popover
cards, toolbars — must **not** use `role=tree`. `tree` is a *navigation/selection* pattern
(roving-tabindex, single-focusable treeitems) and is the wrong model for an *editing* surface whose
nodes contain editable fields and controls. Instead:
- **nested labeled `group`s + `disclosure` buttons** for structure and expand/collapse navigation;
- and the answer to "does a screen-reader user understand the *composition*" — the ticket's real
  question — is that **Motiv already generates a linear text of the structure** (`Reason`: `"(A & B) | C"`,
  and the hierarchical `Justification`). **Expose that generated text as the composition's accessible
  description.** The product's thesis (linearize boolean structure into readable text) doubles as its
  a11y affordance; the visual tree does not have to carry the whole burden alone.

### Sub-1 — target and document

WCAG 2.1 AA is the floor; a **VPAT / Accessibility Conformance Report is an explicit output**, produced
from the audit (dated, versioned) — procurement asks for the document, not the compliance.

### Sub-2 — the other hard surfaces

- **CodeMirror: inherit, don't invent.** CM6 has its own a11y story; the DSL strip uses it rather than
  reimplementing.
- **Command palette:** established `listbox` / `aria-activedescendant` + live-region announcement
  patterns — a known focus/announcement trap, so it is audited manually (below).
- **Modal viewer:** standard dialog focus-trap + return-focus.

### Sub-4 — what enforces it: mechanical + manual, both required

- **Wire `axe-core` into the existing Playwright suite** (missing today) — the mechanical ~50% of AA, in
  CI on every run.
- **A required manual screen-reader pass** (NVDA / VoiceOver) on the four hard surfaces for the half axe
  cannot catch: focus order, announcement quality, meaningful labels on generated content. In scope, not
  optional; the *who* is the maintainer's resourcing call. The **VPAT is produced from both**.

### Sub-5 — the SDK carries none, stated honestly

Ticket 07's headless boundary means accessibility is **entirely the app's**, and the "a11y as inherited
package property" selling point is off the table. The honest corollary to document: an adopter building
their own UI on the headless packages gets **no a11y help** — a real cost of the headless boundary, not
to be hidden.

### Sub-6 — `JustificationTree` is the tractable exception

Read-only rendering of adopter-supplied assertion strings, so the *easy* a11y case (no editing, no
focus-into-fields) — same structure+text approach as the builder, and the **lone place a11y is inherited
from a package** *if it survives* ticket 06's reconsideration (its sibling `RuleTree` is already
removed). If it goes, accessibility is 100% the app's, and `Motiv.Studio`'s read-only justification view
needs the identical treatment.

## Downstream

- **To ticket 06:** `JustificationTree`'s survival decides whether *any* a11y is package-inherited; if it
  is removed like `RuleTree`, the headless boundary is total.
- **New work:** wire `axe-core` into Playwright; the manual screen-reader audit; the VPAT authoring.
- **To the "docs/adoption" fog:** the VPAT is the procurement artifact that fog must surface.
