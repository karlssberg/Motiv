# Builder: Pointer-Driven Node Insertion and a Permanent DSL Strip — Design

**Date:** 2026-07-27
**Status:** Approved (pending spec review)
**Scope:** `ui/apps/demo/src/builder/`, `ui/apps/demo/src/panes/BuilderPane.tsx`,
`ui/apps/demo/src/styles/app.css`, and additive pure functions in
`@motiv/rules-core`. **No DSL grammar, JSON Schema, or server-side change.**

## Problem

The builder can read a rule's structure but cannot author it. The only structural
action it exposes is `Remove`, in the `⋯` menu. Everything else — adding a term,
grouping two terms, reordering operands — requires typing DSL into a row, and
`NodeToolbar` states this as policy rather than as an omission
([`ui/apps/demo/src/builder/NodeToolbar.tsx:8`](../../../ui/apps/demo/src/builder/NodeToolbar.tsx)):

> Nothing here may change the node it belongs to… Composition is authored by
> typing DSL in the node's row.

That policy was a sound response to a real hazard — a control that re-kinds its
own host re-renders the panel hosting it into a panel for something else — but it
leaves the tree read-only to the mouse and to touch entirely. A user who can see
exactly where a term belongs has no way to put it there except by retyping an
expression they can already see.

This design adds pointer- and touch-driven insertion and reordering **without
reversing that policy**: the pointer chooses *position*, the keyboard still
chooses *content*. No control mutates the node that hosts it.

### The premise, corrected

The feature was framed as requiring flattening of `and`/`andAlso`/`or`/`orElse`
subtrees so nodes accept more than two children. That is already true, and has
been since the document model was written:

- [`document.ts:17`](../../../ui/packages/rules-core/src/document.ts) declares
  `and: RuleNode[]` — an array, not a pair.
- [`rule.v1.json:41`](../../../schemas/rule.v1.json) sets `minItems: 2` with no
  maximum.
- [`parser.ts:246`](../../../ui/packages/rules-core/src/dsl/parser.ts) already
  collapses a run of same-operator tokens into one n-ary node: `a && b && c`
  parses to a single `andAlso` of three.

So the model is n-ary and the parser produces n-ary documents. What is missing is
that the **builder never exploits it**: `addOperand` and `wrapInOperator` exist in
[`editor.ts`](../../../ui/packages/rules-core/src/editor.ts) and are called from
no UI code. The remaining flattening work is not a type change but a
**normalization rule** — residual same-operator nesting is schema-valid and
reachable, and nothing collapses it.

## Design Decisions

| Axis | Decision |
|---|---|
| What a gap inserts | An **empty phantom slot** with a focused DSL editor. Never enters the document |
| Resting affordance | A **row-anchored `+`**, joining the existing hover-revealed `⋯`/`📌` cluster |
| `+` on **any** row | Inserts a sibling **immediately after that row** — one rule, no per-kind cases |
| The one slot `+` cannot reach | `⋯ → Insert first operand`, offered on operator rows only |
| Single-child parents | The `+` **wraps in `and`**, unconditionally; retype via the existing `OperatorPicker` |
| Drag affordance | Labelled **drop strips**, materialised only during a drag or an armed move |
| Drop targets | Strips (position) **+ onto a leaf** (wrap in `and`) **+ onto an operator row** (append) |
| Preview | A **permanent one-line DSL strip** above the tree, showing the whole rule |
| Preview during a drag | Same strip, **prospective content** — the rule as it would read after the drop |
| Long rules | Horizontally scrollable; **auto-scroll the relevant span into view**, edge fades |
| Hover | Fills the corresponding span in the strip |
| Selection | **Underlines** the corresponding span — a different axis, so nesting stays legible |
| Scroll priority | **Most-recently-changed wins** — no special case for hover vs selection |
| Normalization | Flatten **undecorated** same-operator nesting, either direction, within the touched subtree |
| Never flattened | Anything carrying `name`/`whenTrue`/`whenFalse`; `xor`; cross-key pairs |
| `xor` with >2 operands | Rendered, but **labelled as parity** — it means "an odd number", not "one of" |
| Keyboard & touch move | **Armed-move** over the drag's own target set, rendered as focusable buttons |
| Input mechanism | **Pointer Events** with a 5px promotion threshold and a persistent leftmost grip |

## What the Codebase Already Provides

This feature is cheap because four things already exist and are already correct.
Naming them is not throat-clearing — each one removes a design choice that would
otherwise have to be made and defended.

**Pure document mutation.**
[`setNode`](../../../ui/packages/rules-core/src/paths.ts) `structuredClone`s and
returns a new document rather than mutating. A candidate document therefore costs
one clone, which is what makes a live preview affordable at all.

**A round-trip-guaranteed printer.** `printInline(node)` renders any node as one
line of DSL, and [`printer.ts:177`](../../../ui/packages/rules-core/src/dsl/printer.ts)
guarantees `parse(printInline(node))` deep-equals `node`. That guarantee is what
licenses the print-then-reparse step the strip needs.

**Both directions of path↔text, already written.**
`rangeOfPath(path, spans, len)` at [`lint.ts:28`](../../../ui/apps/demo/src/dsl/lint.ts)
maps a node path to a text range with ancestor fallback; `innermostSpanAt` at
[`DslEditor.tsx:45`](../../../ui/apps/demo/src/dsl/DslEditor.tsx) is the inverse.
And the parser maintains exactly the invariant these need: parsing `( expr )`
*deletes* the inner node's span and re-records a wider one covering the parens,
[`parser.ts:174`](../../../ui/packages/rules-core/src/dsl/parser.ts), so that
"each path keeps exactly one span". Hovering an `or` node highlights
`(has-premium | is-trial)` **with its parentheses** — which is the reading you
want, since the parens are precisely what an indented tree cannot show.

**A left fold on the backend.** All four binders aggregate operands with
`children.Aggregate((left, right) => left.And(right))` —
[`RuleBinder.cs:128`](../../../src/Motiv.Serialization/RuleBinder.cs),
[`MetadataRuleBinder.cs:121`](../../../src/Motiv.Serialization/MetadataRuleBinder.cs),
and both async variants. This is load-bearing for the normalization rule below,
and it is the fact that makes `xor` different from the rest.

## The Permanent DSL Strip

A single line above the tree, showing `printInline(document.rule)`.

It is permanent rather than drag-only, and that is the most consequential choice
in this design. A strip that appeared only during a drag would be new chrome
introduced mid-gesture — the worst possible moment to ask someone to read a
surface for the first time. Permanent, it means a drag only changes the line's
*content*, and the user has been reading that line all along.

It also completes a pair. An indented tree shows structure but destroys reading
order and precedence; DSL text shows reading order but hides structure. Neither
alone explains why a rule means what it means, which is the same
boolean-blindness argument the library itself is built on. The correspondence
highlight is what makes the pair whole.

**Where it goes.** `.accordion-strip` already reserves `min-height: 26px`
unconditionally ([`app.css:651`](../../../ui/apps/demo/src/styles/app.css)) —
`BuilderPane` documents the reservation as deliberate, so the tree does not jump
when the first node is pinned. The DSL line shares that reserved band, so the
strip costs no new vertical space and introduces no layout shift.

**How it gets spans.** The builder holds a document, not text, and spans are a
parse product. So the strip computes `parse(printInline(document.rule))` and keeps
the `spans` array, memoised on document identity. This is a print-then-reparse
round trip, which is sound rather than expedient: the printer's round-trip
guarantee is exactly the promise that the reparse recovers the same tree. One
print and one parse per document change, on a document of tens of nodes.

A `printWithSpans` in `rules-core` would avoid the round trip. It is not worth
building until measurement says otherwise, and the round trip has the advantage
of being provably consistent with the DSL pane, which parses the same way.

**Overflow.** The strip scrolls horizontally and auto-scrolls the currently
relevant span into view; fades at both edges indicate off-screen content. This
generalises correctly: for a drag the relevant span is the change, for a hover it
is the hovered subtree. Left-truncation falls out as the special case where the
relevant span sits at the end.

## Insertion at Rest

### The row `+`

Every row gains a `+` alongside `⋯` and `📌`. The cluster already exists and is
already hover-revealed ([`app.css:507`](../../../ui/apps/demo/src/styles/app.css)),
so a third member inherits the reveal behaviour, spacing and tab order.

It means one thing on every row: **insert a sibling immediately after me.**

An earlier draft split this by row kind — index 0 on operator rows, after-me
elsewhere — on the theory that the split made every operand slot reachable by
button. It does not, and the reason is arithmetic rather than detail. In
`and: [a, {or: [b, c]}, d]` the `and` has four slots and the `or` has three, so
seven slots are served by six rows. **One button per row can never cover them**,
under any assignment: each row participates in two lists, its parent's and its own
children's, and a single button must pick one. The split bought nothing and cost a
second rule to learn.

So the `+` is uniform, and the resolution of the position it *cannot* reach —
before an operator's first child — is `⋯ → Insert first operand`, offered on
operator rows only. That belongs in the menu anyway: the menu is already where
this builder puts structural actions, and the item is self-labelling in a way a
second glyph on the row would not be.

When the row has no parent list — the root rule, a `not`'s child, a quantifier's
body — "a sibling after me" is expressed by **wrapping in `and`**, producing
`and: [existing, new]`. Always `and`, never inferred: the new parent's
`OperatorPicker` badge renders one click away on the row that just appeared, so
changing it is cheap and uses a control the user already knows.

The uniform rule composes with normalization in a way the split rule did not. A
root that is already `and: [a, b]` wraps to `and: [{and: [a, b]}, new]`, which
normalization — the nested `and` being undecorated — immediately flattens to
`and: [a, b, new]`. So "wrap in `and`" *becomes* "append to my own list" exactly
when that is the sensible reading, and stays a genuine wrap when the inner node
carries a `name` worth preserving.

### The phantom slot

The inserted node has no content yet, and **must not enter the document**.
`rule.v1.json` has no blank-node kind, so a placeholder would be schema-invalid
the instant it reached the JSON pane or `/evaluate`. `NodeDsl` already set the
precedent for this exact situation
([`NodeDsl.tsx:26`](../../../ui/apps/demo/src/builder/NodeDsl.tsx)): *"the invalid
state lives only in the editor, never in the document."*

So a pending insertion is UI state in `BuilderBody` — a `{ parentPath, index }`
pair — rendered as a phantom row hosting a focused CodeMirror instance, scoped to
the row's model type exactly as an existing row's editor is. Commit parses the
buffer and splices the node in through the planner. Escape, or a blur with an
empty or unparseable buffer, evaporates the slot. Undo history never records a
half-node and the JSON pane never blinks.

## Drag

### Targets

Drop strips appear between rows only while a drag is in progress. Because nothing
competes for the vertical space then, they can be generously tall (≥21px) and
fully labelled with the slot they fill — `operand 0 of OR` — which dissolves the
depth ambiguity an indented outline would otherwise create. Two adjacent lists
each render their own trailing strip at their own indent, so the ambiguous
position after a nested group's last child becomes two distinct targets rather
than one guess.

Two further target kinds, both on rows:

- **Onto a leaf row** — replaces that leaf with `and: [leaf, dragged]` in place.
  Same "always `and`" rule as the single-child `+`, reused rather than reinvented.
- **Onto an operator row** — appends into that operator's list.

These two are only tractable because of the strip. Row interiors and strips are
targets 3px apart, and no amount of visual design makes a finger that precise.
What the strip changes is the **cost of missing**: a mis-aim becomes something
you read and correct by moving, rather than something you discover after
committing and undo. On touch this is stronger than on mouse — a drag is
continuous contact, so the preview updates for the whole gesture and only a
release commits.

### Mechanics

**Pointer Events, not HTML5 drag-and-drop.** Not a trade-off: HTML5 DnD does not
fire from touch at all, which disqualifies it against a stated requirement.
Pointer Events unify mouse, touch and pen on one code path, and
`setPointerCapture` keeps events flowing to the origin element after the pointer
leaves it.

Three mechanics are load-bearing:

- `touch-action: none` on the grip, or the browser scrolls the pane instead of
  dragging.
- A **~5px movement threshold** before promoting `pointerdown` to a drag. Without
  it, `+`, `⋯`, `📌` and the DSL row all become unclickable.
- A **dedicated grip**, leftmost, before the chevron. This is forced by existing
  code rather than chosen: `NodeDsl` renders a `flex: 1 1 auto` button that owns
  essentially the whole row background and mounts CodeMirror on focus
  ([`NodeDsl.tsx:154`](../../../ui/apps/demo/src/builder/NodeDsl.tsx)), so a drag
  starting from the row background would fight it for every event.

The grip is **persistent**, not hover-revealed, unlike `⋯` and `📌`. It is the
primary affordance of the whole feature, and an invisible primary affordance is
undiscoverable. Low contrast, always present.

**Dragging a parent takes its subtree.** Dropping a subtree into one of its own
descendants' targets is refused — the planner rejects any target whose path is the
dragged path or is prefixed by it.

## Armed Move — Keyboard and Touch

Select a row, then `⋯ → Move` (or a key binding on the selected row) **arms** a
move. The armed state renders the drag's own target set as real focusable
buttons; the strip previews as focus moves between them; Enter commits, Escape
cancels.

This costs little because the drag already enumerates the targets — armed-move
renders that same list differently, and the strip is driven by "the current
candidate target" without caring whether it came from a pointer or from focus.
One target model, two input paths.

Armed-move is **not only an accessibility fallback**. Long-press-then-drag on
touch competes with scroll, has no hover to preview from before contact, and
commits on a mis-release. Tap `⋯ → Move`, then tap a labelled target, is
reliable, previewable between the taps, and cancellable. The keyboard path and the
better touch path are the same mechanism.

## The Highlight Model

Three sources want to mark the strip. They resolve cleanly because a drag changes
the strip's *content* while hover and selection only *add a mark* — so a drag never
competes, and the only contest is hover versus selection.

They coexist on **different axes**: selection underlines the span, hover fills
behind it. The nesting case — hovering a child of the selected node — is the
normal traffic pattern, not an exotic one, and two marks on the same axis
(a fill inside a box) is two grey rectangles the eye has to pull apart at 11px
monospace. Different axes nest legibly.

That selection's mark is the quieter of the two is correct, not backwards. The
**row** already carries selection's weight with an inset accent bar; the strip's
underline is a secondary confirmation. Hover has no other indicator anywhere, so
its fill is the only evidence it exists. Weight follows necessity, not intent.

`selectedPath` is new UI state in `BuilderBody`, alongside the existing
`openPopover`. It is also what armed-move needs, so one field serves both.

Two resolved details: scroll priority is **most-recently-changed wins**, which
needs no special case; and the highlight **stays active on collapsed rows** even
though a collapsed row shows its own DSL — the duplication is harmless, whereas
suppressing it would invent an inconsistency where the strip mysteriously stops
responding on some rows.

## Normalization

A mutation can produce same-operator nesting — `{and: [{and: [a, b]}, c]}` — which
is schema-valid and which nothing currently collapses. The rule:

> **Flatten undecorated same-operator nesting, in either direction, within the
> subtree a mutation touched.**

**Why direction does not matter.** The backend's left fold means flat `[a,b,c]`
and left-nested `[[a,b],c]` bind to the identical spec tree, so flattening
left-nesting is provably free. Right-nesting folds differently and would change
the hierarchical `Justification` — but an *unnamed* nested group adds a level to
that tree while labelling nothing, so it deepens the explanation without
informing it. Undecorated nesting is not merely cosmetically redundant, it is
explanatorily inert, and preserving it protects nothing.

The DSL agrees: `a & (b & c)` renders parens that carry nothing, and nobody
authors that deliberately.

**Why decoration is the real guard.** Flattening a node that carries `name`,
`whenTrue` or `whenFalse` **destroys that payload** — a dissolved node has nowhere
to put it. That is a correctness rule, not a preference. And the DSL marks the
difference visibly: in `a & (b & c) as "financially able"` the parens are the
scope of the `as` clause, load-bearing rather than noise. `precedenceOf` returns
`ATOM` for a named node precisely because *"a named node is either a postfix
primary or is parenthesised by its own `as` clause"*
([`printer.ts:44`](../../../ui/packages/rules-core/src/dsl/printer.ts)).

**When it runs.** At plan time, on the touched subtree only — never on
`loadDocument`, never document-wide. A hand-authored JSON or a DSL round-trip is
displayed as authored rather than quietly rewritten, and the change stays scoped
to what the user's own gesture created. Because the strip previews the planner's
output, normalization is **visible and consented-to before commit** rather than
silent.

### `xor` is never flattened

The left fold makes n-ary `xor` mean `a.XOr(b).XOr(c)` — **parity**, true when an
odd number of operands are satisfied. It does not mean "exactly one". A flat
three-child `XOR` row invites exactly that misreading, so the builder never
creates one.

It must still *render* one, because `parser.ts` already flattens `a ^ b ^ c` from
the DSL and such documents exist today. Mitigation: a `xor` node with more than
two operands is **labelled as parity** rather than as a bare `XOR`, so the flat
display does not lie about what it computes.

## Architecture

### New, in `@motiv/rules-core` — pure and testable

A planner module. Every function takes a document and returns a **new candidate
document**; none touches the store, history, or the network.

- `planInsert(document, target, node) → RuleDocument`
- `planMove(document, fromPath, target) → RuleDocument`
- `normalizeAt(document, path) → RuleDocument`
- `dropTargetsFor(document, fromPath) → DropTarget[]`
- `isLegalTarget(document, fromPath, target) → boolean`

`DropTarget` is a discriminated union over the three kinds: `{kind: 'slot',
parentPath, index}`, `{kind: 'wrap', path}`, `{kind: 'append', operatorPath}`.

**The preview and the commit call the same planner.** The preview prints its
result; the drop commits it. The preview is therefore not a description of the
mutation — it *is* the mutation, minus the commit, and cannot drift from real
behaviour.

This is also where the two genuinely hard cases live, and why they belong in a
pure function rather than in a component:

- **Removing an operand can collapse its parent** when the count falls to one
  ([`editor.ts:73`](../../../ui/packages/rules-core/src/editor.ts)) — which can
  delete the destination path outright.
- **Indices shift.** Removing `and[1]` and inserting at `and[3]` must resolve
  against a defined one of the two orderings.

`RuleEditorStore` gains one thin method that applies a planner result and commits
it to history. The existing `wrapInOperator`/`addOperand` stay; `wrapInOperator`
stops being dead code, called by the `Wrap` path.

### New, in the demo builder

- `RuleDslStrip` — the permanent line: prints, memoises spans, renders marked
  spans, owns scroll-into-view.
- `DropStrip` — one labelled target; a drop zone during a drag, a button while
  armed.
- `PendingSlot` — the phantom row and its editor.
- `NodeGrip` — the pointer-capture drag source.
- `useDragMove` — the Pointer Events state machine: threshold, capture, current
  target, commit/cancel. One hook owning the whole gesture, so no component holds
  a partial view of it.

`BuilderBody` gains `selectedPath`, a pending-insertion slot, and a drag/armed
model, held the same way `openPopover` already is — centrally, so only one is ever
live.

`RuleNodeEditor` is already near the ceiling of what one file should hold at 172
lines with five concerns. The `+`, the grip and the strips should be composed into
it as components, not inlined, or it becomes the file nobody wants to edit.

## Edge Cases

| Case | Behaviour |
|---|---|
| Drop a subtree into its own descendant | Target rejected; not offered |
| Drop onto the dragged node's current slot | No-op; commit skipped, no history entry |
| Move out of a 2-operand parent | Parent collapses to its survivor, as `removeOperand` already does |
| Phantom slot, unparseable buffer | Refused, message under the field, text left as typed |
| Phantom slot, blurred while empty | Evaporates silently, no history entry |
| Insert while a row is mid-DSL-edit | The edit commits first; a phantom slot never coexists with an open editor elsewhere |
| Drag begins on `+`/`⋯`/`📌` | Threshold not met by a tap, so the button wins |
| Quantifier body / `not` child | Single-child parents: `+` wraps in `and`; no slot strips |
| Catalog still loading | Insertion works; the phantom editor's completion is empty until it lands, as existing rows already behave |
| `xor` with >2 operands from DSL | Rendered with a parity label; targets offered, but drops never merge it |

## Testing

**Planner unit tests (`rules-core`)** carry the weight, since the planner is where
correctness lives. Cases: index shift on same-parent moves in both directions;
parent collapse deleting the destination; normalization flattening left and right
nesting; normalization *refusing* a node with each of `name`, `whenTrue`,
`whenFalse`; `xor` never merged; `and`/`andAlso` never merged; illegal
self-descendant targets; `dropTargetsFor` enumerating every slot exactly once.

**Round-trip property test:** for a generated document and a legal target,
`parse(printInline(planMove(...).rule))` deep-equals `planMove(...).rule` — the
planner never produces a document the DSL cannot express.

**Component tests (`rules-react` / demo)**: the `+` inserting at the documented
index per row kind; the phantom slot never reaching the store on cancel; the
strip's marked range matching `rangeOfPath` for a hovered path; armed-move
committing on Enter and cancelling on Escape.

**E2E (`apps/demo/e2e`)** for what only a real browser shows, following the
precedent in `e2e/operator.spec.ts` — which exists because a `visibility: hidden`
element refuses focus in a browser but not in jsdom. Needed here for: the 5px
threshold not swallowing clicks; `touch-action: none` preventing scroll during a
touch drag; `setPointerCapture` surviving a pointer that leaves the row; and the
strip's scroll-into-view actually scrolling.

Per `CLAUDE.md`, the full solution test suite runs before this is considered
complete — the example projects assert on justification strings, and normalization
touches structure that shapes them.

## Suggested Implementation Order

Two milestones, each independently shippable and useful on its own. The split is
along input mechanism, because the planner and the strip are shared by both and are
worth having under test before a pointer state machine sits on top of them.

**Milestone 1 — the planner, the strip, and click insertion.** `planInsert`,
`normalizeAt`, the `xor` parity label, `RuleDslStrip` with hover and selection
highlighting, `selectedPath`, the row `+`, and `PendingSlot`. Delivers authoring by
mouse, touch and keyboard with no drag at all, and puts the correspondence
highlight in front of the user — which is independently valuable.

**Milestone 2 — drag and armed move.** `dropTargetsFor`, `planMove`,
`isLegalTarget`, `DropStrip`, `NodeGrip`, `useDragMove`, and `⋯ → Move`. Both
reuse Milestone 1's strip as their preview surface, which is why the strip is not
a drag feature and should not be built as one.

## Out of Scope

- **DSL grammar, JSON Schema, and server-side.** Unchanged. The parser's existing
  `xor` flattening is left alone; it is a pre-existing question, not one this
  feature introduces.
- **Side-by-side builder and DSL pane** with live correspondence, replacing the
  current toggle. The strip is a stepping stone toward it, not a dead end, but the
  layout change is its own piece of work.
- **`printWithSpans`** in `rules-core`, avoiding the strip's print-then-reparse.
  An optimisation to make when measurement asks for it.
- **Clicking DSL text to select the corresponding node** — the inverse
  correspondence. `innermostSpanAt` already exists, so it is nearly free, but it
  is a separate interaction with its own questions.
- **An explicit `⋯ → Flatten`.** The automatic rule covers the common case; this
  would only serve someone cleaning up branches they are not otherwise touching.
- **Drag between the builder and the DSL pane**, or from an external palette.
