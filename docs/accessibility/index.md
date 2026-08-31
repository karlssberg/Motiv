---
title: Accessibility
description: Motiv Studio's WCAG 2.1 AA target, the Accessibility Conformance Report that records it, what axe-core and the keyboard suite enforce automatically, the manual screen-reader audit that covers the rest, and the honest statement that the headless packages carry no accessibility of their own.
---

Motiv Studio targets **WCAG 2.1 Level AA**. This page is the conformance record: what is enforced
mechanically, what is owed to a manual audit, and where the target is not yet met.

Accessibility is a procurement gate for the buyers this product is aimed at — public sector
especially — so the deliverable is a *document*, not only a set of fixes. What follows is that
document, kept beside the code so it is revised by the same change that revises the behaviour.

## The key move: explanation is the affordance

The hard part of an authoring UI for boolean logic is not the buttons. It is the **composition** —
whether a reader who cannot see the indentation, the connecting lines and the nesting can tell what
the rule actually says.

Motiv already answers that. Turning boolean structure into readable text is the whole point of the
library: `Reason` linearises a composition (`"(A & B) | C"`), `Justification` renders it as a tree,
and the DSL printer produces the same expression the rule builder is editing. **Studio uses that
generated text as the accessible description of the structure**, at every level:

| Where | What is announced |
|---|---|
| The builder as a whole | `group` named *rule composition*, described by the DSL strip's one-line expression |
| Each parent's operands | `group` named by that subtree's own generated expression — `customer.is-active & customer.is-adult` |
| Each explanation node | `group` named by the assertions it explains |
| The whole explanation | `group` named *why this rule was satisfied* / *not satisfied* |

The text is bounded before it is used as a name (`accessibleExpression`, 120 code points, then an
ellipsis), because a group's name is announced on *entering* it and a thousand-operand rule is not
something to sit through.

### Why the builder is not a `tree`

`role="tree"` is a navigation and selection pattern: one focusable item at a time, arrow keys to
move, and items that are chosen rather than edited. The rule builder is an *editing* surface —
every row holds an inline text editor, a popover, a toolbar and several controls — so a tree's
roving tabindex would fight the fields inside it.

Studio uses **nested labelled `group`s plus `disclosure` buttons** instead. A caret carries
`aria-expanded`, and while its group is on screen it carries `aria-controls` naming it; when the
group is collapsed and unmounted the attribute is dropped, because a reference to an element that
is not in the document is an invalid relationship rather than a harmless one. The read-only
explanation tree (`JustificationTree`) uses the same shape.

### Where a `tree` *is* the right model — and what wearing the role costs

The command palette's namespace browser is the opposite case: it browses and selects, and nothing
in it is edited. So it is a real `role="tree"`, and it implements the pattern rather than
approximating it — **one tab stop** with a roving `tabindex`, arrow keys between rows (into a
subtree with `ArrowRight`, back out with `ArrowLeft`), `Home`/`End`, type-ahead on the segment
names, and `Enter`/`Space` to choose. Nothing collapses, so `aria-expanded` is always `true` and
says only what is true.

That is the rule this report holds itself to: **a role is a promise about behaviour, and declaring
one you do not implement is worse than declaring none.** A row that announces as a treeitem sends a
screen-reader user into a navigation mode; if the arrow keys then do nothing, the surface is harder
to use than the plain list it could have been. `axe-core` cannot see the difference — the markup is
well-formed either way — which is why the keyboard suite below exists.

## What enforces it

Both halves are required. Neither is sufficient, and the
[Accessibility Conformance Report](vpat.md) says which evidence each criterion rests on.

### Mechanical — `axe-core` and the keyboard suite, on every run

`ui/apps/studio/e2e-a11y/axe.spec.ts` runs `axe-core` against the built application, gated on the
`wcag2a`, `wcag2aa`, `wcag21a` and `wcag21aa` rule tags. The `accessibility` job in
`.github/workflows/ui.yml` runs it on every push and pull request.

It scans on two axes, because a scan that only visits routes checks none of the surfaces that
matter:

- **Every view** — the rules page, the builder holding a composition, an evaluation with its
  justification, the DSL surface, the propositions page (browsing and with one selected), and the
  admin grants page.
- **Every hard surface in the state it is hard in** — the command palette browsing, filtered, and
  filtered to nothing; the modal document viewer; the operator picker's open listbox; a row's
  actions menu; a node's detail panel.

Both in **light and dark colour schemes**, since the stylesheet defines two palettes and a contrast
ratio that holds in one says nothing about the other.

#### Keyboard behaviour — the half a scan cannot see

`ui/apps/studio/e2e-a11y/keyboard.spec.ts` drives the keyboard against the same built bundle, in
the same job. A scan judges markup; this judges what the markup *promised*: that the namespace tree
is one stop in the tab sequence rather than one per proposition, that the arrow keys, `Home`/`End`
and type-ahead move between its rows, that a proposition can be chosen without a pointer at any
point, and that the page switcher's links carry `aria-current` and navigate on `Enter`.

It is a real browser on purpose. jsdom has no tab sequence of its own, so a unit test can assert
which row *would* be reached and only this can assert that `Tab` reaches it.

Run both locally with `pnpm --filter @motiv-rules/studio a11y`. They need no backend: the suite
serves the built bundle and answers the API from fixtures, so findings are facts about the markup
and its behaviour rather than about whatever a live store happens to hold.

Between them these still catch roughly half of AA. What they cannot see is the other half.

### Manual — the screen-reader pass

Required, not optional, and **not yet performed** (see [Status](#status)). The script below is what
it covers; it exists so the audit is repeatable and so its result can be dated against a commit.

Run with **NVDA + Firefox** or **VoiceOver + Safari**, keyboard only, on each of the four surfaces
the mechanical suite cannot judge:

**1. The accordion builder**

- Tab through a composite rule from the DSL strip down. Does the reading order match the visual
  order, and does focus ever land somewhere invisible?
- Enter a nested group. Is the announced name the subtree's expression, and does it describe what
  is inside?
- Collapse a subtree from its caret. Is the state change announced, and is focus retained?
- Open a node's detail panel and edit `whenTrue`. Is the field's purpose clear from its name alone?
- **The question the whole audit exists for:** from the announcements alone, can the listener say
  what the rule means?

**2. The CodeMirror DSL strip**

- Confirm the editor is announced as a named text box (*rule DSL*) and not as an unlabelled edit
  field.
- Trigger completion. Are candidates announced, and is the selected one identifiable?
- Introduce an error. Is the diagnostic reachable and announced?

**3. The command palette**

- Open it. Does focus land in the search box rather than on Close?
- Tab into the namespace tree and arrow through it. Is each row announced with its badges, is the
  nesting audible as nesting, and does the announced position match where the focus ring is?
- Type. Is the result count announced as it changes, and is the highlighted row announced as the
  arrow keys move over it?
- Filter to nothing. Is the empty result *stated*, rather than silent?
- Escape. Does focus return to the control that opened the palette?

**4. The modal document viewer**

- Open it. Is the dialog announced with its name, and is the content behind it inert to the virtual
  cursor?
- Tab through it. Does focus stay inside?
- Escape and backdrop click both dismiss. Does focus return to the opener in both cases?

The report's [*What the manual pass still owes*](vpat.md#what-the-manual-pass-still-owes) section is
the worklist: one entry per outstanding criterion, naming what this pass has to establish before that
criterion can be answered.

Findings are **not** written into that report — it is generated, and editing it by hand trips the
drift gate. They go into the record it is generated from, `ui/apps/studio/a11y/conformance.ts`: on
the criterion's row, replace the `manual` evidence entry with what the pass established (a
`reasoned` argument, or a verdict of *Does Not Support* with the defect named), move the verdict off
*Not Evaluated*, and put the audit date and the commit audited in the remark. Then regenerate:

```bash
pnpm --filter @motiv-rules/studio a11y:report
```

The report and the worklist above both follow from that one edit, which is the point — an audit
result recorded in the document alone would be a claim with nothing behind it, and the record's gate
would refuse it on the next run.

A verdict of *Does Not Support* will additionally fail the check that guards the summary sentence in
[Status](#status). That is deliberate rather than an obstacle: it means the audit has contradicted a
claim this page makes, and the page has to be rewritten in the same commit that records the finding.

## Status

**Partial. The mechanical half is enforced and green; the manual half is scripted and outstanding.**

The full record is the [Accessibility Conformance Report](vpat.md): every WCAG 2.1 Level A and AA
success criterion — all fifty — with a verdict and the evidence it rests on. This page summarises it;
that page is the document a procurement process asks for.

**No criterion is reported as failing, and a good many are reported as not evaluated** — which is
not the same thing, and the difference is the point. The report's summary carries the tally; it is
not repeated here, because a count restated in prose is a count that drifts.

That distinction is stated plainly because the alternative is worse: a conformance report that
implied an audit nobody ran would be a false claim in a document buyers rely on.

### The report is generated, and its mechanical claims are checked

The report is not written. It is rendered from a record in `ui/apps/studio/a11y/conformance.ts`, and
`ui/apps/studio/test/a11y/conformance.test.ts` refuses a record whose claims the suites do not
support — a row claiming axe coverage where axe has no rule for the criterion, a row omitting axe
coverage the sweep does run, a row citing a keyboard test that does not exist, a verdict of
*Supports* resting on nothing but an owed manual pass, or a published document that has drifted from
the record. A row never names an axe rule: it claims that axe covers the criterion, and which rules
that resolves to is read from axe's own tags when the record is checked and when the report is
rendered. So a claim cannot outlive the rule it rests on, and an axe upgrade that drops one shows up
as a changed report rather than as a sentence nobody re-read.

That gate is not ceremony. It was written because the hand-maintained table it replaced was wrong in
both directions, and nothing could have noticed:

- **1.4.11 Non-text Contrast** was published as *"Supports — Enforced by axe"*. axe-core has no rule
  for that criterion at any version: `color-contrast` is tagged `wcag143`, which is text contrast
  only. Non-text contrast — control boundaries, focus indicators, the builder's state marks — had
  never been checked by anything. It is now **Not Evaluated**, and it is the sharpest item the manual
  pass owes.
- **1.4.4 Resize Text** was published as *"Owed — not covered by the mechanical suite"*, while
  `meta-viewport` is tagged `wcag144` and had been running in the sweep all along. Under-claiming is
  the quieter fault and the more dangerous one: coverage the report omits is coverage nobody would
  notice losing.

### And enumerating the criteria found a defect the sweep could not

The table this replaced answered for fourteen criteria and dismissed the rest in a sentence.
Answering for all fifty meant answering for **2.4.2 Page Titled** — which the sweep had always passed,
because axe's `document-title` rule asks only that a title exist. Studio is a single-page application
with one `<title>` in `index.html`, so all three routes shared the name *Motiv Studio*: the two places
a title is actually used — what a screen reader announces when the route changes, and how a user tells
Studio entries in their history apart — got nothing. `src/routing/useDocumentTitle.ts` now writes the
title from the route, selection first, and is unit-tested.

## The SDK carries no accessibility

Stated plainly because it is a real cost of the headless package boundary, not a footnote:
**`@motiv-rules/core` and `@motiv-rules/react` ship no components, so an adopter building their own
UI inherits no accessibility from them.** Everything above is Motiv Studio's, and an adopter who
builds a different authoring surface owes all of it themselves.

Two things do carry over:

- **`JustificationTree`** (in `@motiv-rules/react`) is the exception. It is read-only, so it is the
  tractable case, and it owns its own structure: nested labelled groups, each named by the
  assertions it explains, with a render prop for the visible markup and a group id handed to the
  consumer so a disclosure control can name what it toggles. It is the lone place accessibility is
  inherited from a package — and it is inherited **only by React**. An adopter on another runtime
  gets the decision from this page and writes the markup themselves;
  [the worked Vue adapter](https://github.com/karlssberg/Motiv/tree/main/ui/examples/vue-adapter)
  ports it and is tested against the same behaviours, so what that costs is a measured row on the
  price table in [Runtimes and Support Tiers](../adoption/index.md#other-javascript-frameworks)
  rather than an estimate.
- **`accessibleExpression`** (in `@motiv-rules/core`) is the generated text itself, bounded for use
  as an accessible name. It renders nothing, so it does not breach the boundary — but it means an
  adopter's own UI can reuse the key move above rather than reinvent it.
