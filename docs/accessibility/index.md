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

Both halves are required. Neither is sufficient, and the report below says which evidence each row
rests on.

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

Record findings against the criteria below, date the result, and note the commit audited.

## Status

**Partial. The mechanical half is enforced and green; the manual half is scripted and outstanding.**

This is stated plainly because the alternative is worse: a conformance report that implied an audit
nobody ran would be a false claim in a document buyers rely on. The rows below say which evidence
each rests on.

No row now rests on an approximation: the two that read *Partially Supports* did so because a role
promised behaviour the markup did not deliver, and both roles have since been made true — one by
implementing the pattern, one by dropping a role that was never the right one. What is still owed
is a person with a screen reader, which is a different kind of evidence and not one any suite can
stand in for.

| Criterion | Level | Conformance | Evidence and remarks |
|---|---|---|---|
| 1.1.1 Non-text Content | A | Supports | Icon-only controls carry `aria-label`; decorative marks are `aria-hidden`. Mechanical. |
| 1.3.1 Info and Relationships | A | Supports | Nested labelled `group`s carry the composition; disclosures name what they control while it is mounted. Mechanical, plus **owed** manual confirmation that the announced structure is *comprehensible*. |
| 1.3.2 Meaningful Sequence | A | Owed | Reading order is a manual-audit question. |
| 1.4.3 Contrast (Minimum) | AA | Supports | Enforced by axe in both colour schemes, on every view and open surface. |
| 1.4.4 Resize Text | AA | Owed | Not covered by the mechanical suite. |
| 1.4.11 Non-text Contrast | AA | Supports | Enforced by axe. |
| 2.1.1 Keyboard | A | Supports | Every control is reachable and operable, and the one surface that declared a keyboard pattern now implements it: the palette's namespace tree has a roving tabindex, arrow-key movement, `Home`/`End` and type-ahead. Enforced by the keyboard suite. |
| 2.1.2 No Keyboard Trap | A | Supports | The modal is a native `<dialog>` opened with `showModal()`, so the trap and its release are the platform's. |
| 2.4.2 Page Titled | A | Supports | Mechanical. |
| 2.4.3 Focus Order | A | Owed | Manual-audit question. |
| 2.4.7 Focus Visible | AA | Owed | Not judged mechanically. |
| 3.3.2 Labels or Instructions | A | Supports | Every input carries a name, including the CodeMirror content elements, which name themselves rather than inheriting one. |
| 4.1.2 Name, Role, Value | A | Supports | Names, roles and states are present throughout and checked mechanically. Every declared role is now honoured: the palette's tree implements the tree pattern, and the page switcher — which controls no panel — is a `<nav>` of links carrying `aria-current` rather than a `tablist`. |
| 4.1.3 Status Messages | AA | Supports | The palette's result count is a `status` region; errors are `alert`s. **Owed** manual confirmation that the announcements are useful rather than merely present. |

Criteria not listed are either not applicable to this application (no audio, video, or timed
content) or are inherited from the platform.

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
  inherited from a package.
- **`accessibleExpression`** (in `@motiv-rules/core`) is the generated text itself, bounded for use
  as an accessible name. It renders nothing, so it does not breach the boundary — but it means an
  adopter's own UI can reuse the key move above rather than reinvent it.
