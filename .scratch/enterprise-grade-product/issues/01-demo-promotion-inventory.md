# Inventory the demo's UI for promotion

Type: task
Status: resolved
Blocked by: —

## Question

Nothing to decide here — this produces the fact that ticket 07 needs and cannot be decided without.

`ui/apps/demo/src` is 6,819 lines against `@motiv/rules-react`'s 218. Classify every file in the
demo as **product-generic** (any serious rules app would need this) or **demo-specific** (exists to
show off, or encodes this demo's particular choices).

Cover at minimum:

| Area | Lines |
|---|---|
| `builder/` — the accordion editor, 19 files | 1,680 |
| `dsl/` — CodeMirror language, completion, hover, lint, payload chips, popovers | 1,251 |
| `panes/` — RuleHeader, Builder, Evaluate, Checkout, Editor, Propositions, Document modal | 1,103 |
| `explorer/` — PropositionExplorer, PropositionDialog, DependentsStrip | 607 |
| `shell/` — Toolbar, CommandPalette, Modal, icons | 437 |
| `routing/`, `styles/`, `App.tsx`, `main.tsx`, `decorationPatch.ts` | ~240 |

For each file record: the classification, the lines, what it imports from `@motiv/rules-core` /
`@motiv/rules-react`, what it imports from CodeMirror or other third-party UI, and — critically —
whether it is **coupled to the demo's specific model** (`Customer`, `Order`, the checkout pane) or
is model-agnostic.

Flag anything that is product-generic *but* would drag a heavy dependency into a package if promoted
(the CodeMirror surface is the obvious candidate: `@codemirror/*` is six packages in the demo's
dependencies and would become a peer dependency of any package that promoted `dsl/`).

### Answer shape

A table, plus a short narrative on the two or three files whose classification is genuinely
arguable. Do **not** recommend a boundary — that is ticket 07's job. Produce the evidence.

## Answer

Full inventory: [demo-promotion-inventory.md](../research/demo-promotion-inventory.md).

### Headline

54 files, 6,819 lines: **product-generic 4,459 (65%, 41 files)**, arguable 584 (8), demo-specific
1,776 (5). But the demo-specific bulk is a single file — `styles/app.css` at 1,537 lines. **Excluding
both stylesheets, only 239 of 5,238 TypeScript lines (4.6%) are genuinely demo-specific**:
`App.tsx`, `main.tsx`, `CheckoutPane.tsx`, `RulesPage.tsx`.

### Model coupling is nearly absent

One constant — `MODEL_TYPE = 'customer'` in `App.tsx`, imported by **five files**, four of them in a
single expression each. `Order`, `loyalty-discount`, `can-checkout` and `fraud-screening` appear
**nowhere** in `src/` — only in the README and e2e specs. `shell/` (437) and `explorer/` (607) have
zero coupling of any kind; `shell/` imports neither Motiv package.

So the intuition that the demo is welded to its model is **wrong**. Threading `modelType` as a prop
is a five-site mechanical change.

### The real blockers are different

1. **Styling has no packaging story.** Every component emits unscoped class names into a 1,537-line
   global stylesheet. This, not model coupling, is what makes components non-promotable as written.
2. **1,058 lines sit behind six `@codemirror/*` packages.** Both npm packages currently have zero
   runtime dependencies beyond React and rules-core.

### CodeMirror is more separable than assumed

Six files import it at runtime (777 lines); **three touch it type-only** — `completion.ts` (90) and
`lint.ts` (64) import only the `Completion` / `Diagnostic` shapes. So completion sources and lint
diagnostics **can** be exported without a CodeMirror dependency. The highlight grammar cannot:
`motivLanguage.ts` is a `StreamParser` plus `@lezer/highlight` by construction — but
`builder/dslTokens.ts` (29 lines, no CM import) is the existence proof of a CM-free highlighter over
core's `tokenize`. Ticket 07's "third option" is therefore real, not hypothetical.

### `dsl/` is integration, not reimplementation — with exceptions

~1,052 of 1,251 lines are genuine editor integration over core's parser. ~199 are duplication:
`motivLanguage.ts:52-89` re-derives `tokenize`'s classification as a `StreamParser` (near-identical
prose for both numeric edge rules), and **the DSL vocabulary exists in three copies** because core's
`KEYWORDS` / `TYPES` / `QUANTIFIERS` are module-private. `rules-core/src/dsl/lexer.ts:8-14` documents
that this exact copying has already drifted once. Also duplicated: `usePopoverCard` vs
`useAnchoredCard` — two hooks over one `placePopover` with an identical private helper.

### The 218-line question, answered empirically

**Small because nothing pushed on it.** The sharpest datum: `RuleTree` — the one non-trivial
component `@motiv/rules-react` exports — is **imported by nothing in the demo**. The app wrote its own
292-line `RuleNodeEditor` instead. The package's flagship component was bypassed by its only consumer.

### Documentation drift found in passing

`ui/apps/demo/README.md` claims load-bearing seam comments are under `builder/` — **there are none
there**. The five `Seam:` comments are in `App.tsx`, `RuleHeader.tsx`, `CheckoutPane.tsx` and
`RulesPage.tsx`. The README also references `panes/JsonPane.tsx`, which no longer exists.
