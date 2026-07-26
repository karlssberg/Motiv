# Builder: Inline-DSL Rows and a Pinnable Metadata Accordion — Design

**Date:** 2026-07-26
**Status:** Approved (pending spec review)
**Scope:** `ui/apps/demo/src/builder/`, `ui/apps/demo/src/panes/BuilderPane.tsx`,
`ui/apps/demo/src/styles/app.css`, and one additive export in
`@motiv/rules-core`'s DSL printer. No backend or serialization change.

## Problem

The builder does not behave like the accordion its own spec describes, and the
gap is not cosmetic — it is that a single `expanded` flag is being asked to mean
two unrelated things.

Today `BuilderBody` seeds every path down to `MAX_EXPAND_DEPTH = 5` as expanded,
so nothing is actually collapsed on load. That one flag gates both a node's
child rows *and* its `DecorationEditor`, while `NodeToolbar` and `QuantifierNode`
render unconditionally beneath every row. The result is a wall of controls: the
expression's shape — the thing a reader is scanning for — competes with five
wrap buttons and a spec select at every level.

Two separate concerns are tangled here:

- **Structure** — how much of the tree is on screen.
- **Detail** — the per-node metadata and edit controls.

They want opposite defaults. Structure should be *open*, because the expression
is the point. Detail should be *closed*, because it is per-node reference
material you consult one node at a time.

## Design Decisions (settled during brainstorming)

| Axis | Decision |
|---|---|
| What the accordion governs | Per-node **detail panel** only — never the child rows |
| Structure default | **Expanded.** The caret collapses a subtree; it does not gate detail |
| Detail default | **Closed.** At most one unpinned panel open at a time |
| Multi-open | Achieved by **pinning** individual nodes, not a single/multi mode switch |
| Close all | A strip above the root, shown only while something is pinned |
| Collapsed subtree | Renders as **one line of editable DSL**, not a static summary |
| Caret semantics | A **representation toggle**: tree view ⇄ DSL text view |
| Leaves | Permanently in DSL view — an inert bullet, no caret |
| Inline editor | One **CodeMirror instance**, mounted only in the focused row |
| Completion scope | Filtered to the row's **model type** — better-scoped than the DSL pane |
| Spec picker | **Removed.** Completion replaces it |
| `+ quantifier` | **Removed.** Completion offers the five quantifier keywords |
| NOT / wrap / add operand / remove | **Kept**, inside the detail panel |
| New operands | Inserted as today, then **auto-focused with text selected** |

## The Two Concerns, Separated

`RuleNodeEditor` currently derives everything from one `expanded(path)` boolean.
It splits into two independent pieces of demo-local UI state:

```ts
collapsed: Set<string>       // structure. empty by default → all subtrees open
openPath:  string | null     // the one transient detail panel
pinned:    Set<string>       // detail panels exempt from displacement
```

A node's detail panel is open when `openPath === path || pinned.has(path)`.
The two concerns never interact: a node may be structurally collapsed with its
detail panel open, or expanded with it closed.

### Pinning

Pinning replaces a single/multi mode switch. The mode is inferred rather than
set, so there is no mode to get stuck in, and it is per-node — you keep the
"one place to look" default while promoting exactly the nodes you are comparing.

| Action | Effect |
|---|---|
| Open a node | Becomes `openPath`, displacing the previous transient. Pinned panels untouched |
| Pin | Moves into `pinned` and frees the transient slot, so the next open does not displace it |
| Unpin | Becomes the transient — stays open, displacing whatever was transient |
| Close a pinned node | Unpins *and* closes. There is no pinned-but-closed state |
| Close all | Clears `openPath` and `pinned` |

Unpinning deliberately keeps the panel open: clicking a pin should never make
content vanish.

`pinned` and `collapsed` are keyed by path, and paths shift when operands are
removed (`$.rule.and[1]` becomes `$.rule.and[0]`). Stale entries are inert —
they address nodes that no longer exist — matching how the current `expanded`
set already behaves. No pruning.

### Close all

A thin strip above the root, rendered inside `BuilderBody` so it travels into
both hosts (`EditorPane` and the standalone `BuilderPane`). It appears only
while `pinned.size > 0`, reading `{n} pinned · close all`. Its height is
reserved so the tree does not shift when it appears.

## The Caret as a Representation Toggle

This is the change that makes the rest cohere. A collapsed subtree does not
hide itself behind a summary — it *becomes* one line of DSL:

```
▾ AND   all must hold · as "quota"          ← expanded: tree view
    • is-active
    ▸ is-verified | !is-flagged             ← collapsed: DSL view, editable
    ▸ atLeast(@minOrders) in orders { is-positive & is-recent }
```

The operator badge and plain-language gloss belong to the *expanded* row. A
collapsed row drops both: the DSL text already says `|` and `!`, so a badge
reading `OR` beside it is noise, and the text needs the full row width.

This is the pane-level Builder/DSL toggle applied recursively per node, which
is also what makes the row editable for free — if a row *is* DSL text, editing
it is just editing text.

**A leaf is permanently in DSL view.** `printBody` renders a spec node as its
bare name, so a leaf's tree form and its text form are the same string — there
is nothing to toggle between, and it gets an inert bullet where a parent gets a
caret. This is not a special case bolted on; it is what keeps DSL view
*universal*, which the authoring story depends on. `+ operand` seeds a leaf, so
if leaves were excluded from DSL view there would be no way to type a new
expression at all.

### `printInline` (new, `@motiv/rules-core`)

`print(document)` cannot serve this: it takes a whole document, and
`isMultiline` forces any node containing a quantifier across several lines with
a block body.

Add `printInline(node: RuleNode): string` to `dsl/printer.ts`. It is the
existing `printNode` with a layout flag threaded through the internal
functions — `print` passes `'block'`, `printInline` passes `'inline'`. Under
`'inline'`, `isMultiline` is treated as false everywhere and `printQuantifier`
emits `${head} { ${body} }` on one line. `dsl/index.ts` already does
`export * from './printer.js'`, so no export wiring is needed.

It belongs in core, next to the printer tests, rather than as a bespoke
renderer in the demo: operator precedence and the parenthesisation rules
(`operandNeedsParens`, `CONNECTIVE`) are subtle and must not drift from the
grammar.

**Invariant:** `parse(printInline(node)).document.rule` deep-equals `node`.
This is what makes a row safely round-trippable, and it is the property the
tests assert.

### Truncation

Pure CSS on the read-state row:

```css
.node-dsl {
  flex: 1 1 auto;
  min-width: 0;          /* without this a flex child will not shrink below content */
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
```

No JS measurement, no reflow loop. `min-width: 0` is load-bearing — `.node-row`
is a flex container, and a flex item's default `min-width: auto` floors it at
its content width, so the ellipsis never engages without it.

## Row Anatomy

Three sibling controls. The row cannot be one large button containing the caret
and pin, because nesting interactive elements is invalid HTML and breaks
keyboard traversal.

```
[caret btn]  [── row body ──]  [pin btn]
```

- **Caret** — only when the node has children. `aria-expanded`, labelled
  `collapse {path}` / `expand {path}`. Toggles `collapsed`. A leaf renders an
  inert bullet in the same slot, so rows stay aligned down the tree.
- **Row body** — the badge + gloss + name when expanded, or the DSL text when
  collapsed. **Not interactive.** It hosts a text editor in DSL view, and
  interactive content nested inside a button is invalid HTML that swallows
  events, so the detail toggle cannot be the row itself.
- **Details toggle** — `aria-expanded`, `aria-controls="detail-{path}"`,
  labelled `details for {path}`. Opens the detail panel.
- **Pin** — `aria-pressed`, labelled `pin {path}` / `unpin {path}`.

The details toggle and pin are always rendered but surfaced only on row hover,
on focus, or while open or pinned — so a resting tree reads as structure rather
than a grid of buttons, without either control becoming keyboard-inaccessible.

## The Inline Editor

Only one row is ever in edit state, so this is a **single CodeMirror instance**
mounted into the focused row — the same cost as the DSL pane, and it inherits
that pane's highlighting, completion, lint and hover extensions.

Read-state rows do not carry an editor. They render `printInline(node)` as
static highlighted spans built from `tokenize(text)`, mapping each token's
`kind` to a `.tok-{kind}` class. The existing token colours in `theme.ts` are
the source of truth for those classes.

**Lifecycle**

| Event | Behaviour |
|---|---|
| Focus / click a row in DSL view (any leaf, or a collapsed parent) | Mount the editor with the full untruncated text |
| Enter, or blur | Commit |
| Escape | Revert to the node's current `printInline` and unmount |
| Commit | `parse(text)` → on success `store.replaceNode(path, document.rule)` |
| Parse error | Error renders on the row, commit is blocked, text stays as typed |

Blocking the commit on a parse error is what keeps the document always valid;
the invalid text lives only in the editor's local buffer, exactly as the DSL
pane's uncommitted buffer does.

### Completion scoping

`createMotivCompletion(getCatalog)` offers `catalog.specs` wholesale. The spec
picker it replaces is model-type scoped
(`catalog.specs.filter(s => s.modelType === modelType)`, `NodeToolbar.tsx:29`),
and a quantifier body is scoped to the collection's `elementModelType`.

The inline row already knows its `modelType` — `RuleNodeEditor` computes the
element-scoped type for quantifier children today. It passes a **pre-filtered
catalog** into the completion source, so inline authoring is scoped at least as
tightly as the picker it removes. The DSL pane's own unscoped completion is a
pre-existing gap and stays out of scope.

### Parameter references

`parseCount` accepts `@ident` as a `paramRef` without checking it against any
declaration (`parser.ts:98`), so a subtree containing `atLeast(@minOrders)`
parses standalone — no need to prepend the document's `param` block. Because
the commit splices back into the whole document, document-level validation
still sees the declarations.

## Toolbar Changes

The detail panel holds `DecorationEditor` plus the *structural* operations.

**Kept** — NOT, wrap (AND/OR/XOR/AndAlso/OrElse), add operand, remove. Each is
one click on an existing node versus retyping it, and they are what keeps the
builder distinct from the DSL pane. The builder's value over that pane is
structural navigation, per-node metadata, and scoped editing — one subtree at a
time with the rest as context.

**Removed** — the catalog spec select, fully replaced by better-scoped
completion; and `+ quantifier`, since completion already offers `all`, `any`,
`exactly`, `atLeast` and `atMost` from `DSL_QUANTIFIERS`.

The disabled `expression — coming` extension point stays as-is.

### Adding an operand

`+ operand` inserts as it does today (seeded with the first in-scope catalog
spec). The new node is a leaf, so it is already in DSL view; the builder focuses
its editor and selects the text, so typing replaces it.

A "draft operand" living outside the document until committed was considered
and rejected: it buys an empty row instead of a selected one, at the cost of a
state machine and a window where the tree and the document disagree.

## Testing

**`@motiv/rules-core`** — unit tests beside the existing printer tests:
quantifiers rendering on one line, precedence parens preserved, `as "name"`
clauses, and the round-trip invariant
`parse(printInline(node)).document.rule === node` over the existing
`dsl-roundtrip.test.ts` corpus.

**`ui/apps/demo`** — accordion behaviour: opening displaces the transient but
not a pinned panel; pin frees the transient slot; unpin keeps the panel open;
closing a pinned node unpins it; close-all clears both and its strip appears
only while something is pinned. Inline editing: a collapsed row shows
`printInline` text; a valid edit commits through `replaceNode`; an invalid one
surfaces an error and leaves the document untouched; Escape reverts.

**Known churn.** Moving the toolbar into the detail panel breaks every test
that drives those controls without first opening a node:
`RuleNodeEditor.test.tsx`, `QuantifierNode.test.tsx`, `ExtensionPoints.test.tsx`,
and the `smoke`, `higher-order`, `dsl` and `live-rules` e2e specs. Removing the
spec select additionally invalidates every `spec at $.rule` selector, which all
four e2e specs use as their "builder is ready" wait. They need a new readiness
selector and a DSL-typing path in place of `selectOption`. This is mechanical
but it is the bulk of the diff, and it should be treated as first-class work
rather than fallout.

## Out of Scope

- The DSL pane's unscoped completion.
- Pruning stale `pinned` / `collapsed` paths after structural mutation.
- Syntax highlighting inside the *editing* row beyond what CodeMirror already
  provides.
- Any backend, serialization or `parameters` support change.
