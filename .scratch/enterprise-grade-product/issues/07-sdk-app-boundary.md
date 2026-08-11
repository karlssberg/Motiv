# Where does the SDK/app boundary sit?

Type: grilling
Status: resolved
Blocked by: 01

## Question

The single most consequential decision on this map. Every UI ticket downstream is shaped by it.

**Measured today:** `ui/apps/demo/src` is 6,819 lines; `@motiv/rules-core` is 2,079;
`@motiv/rules-react` is **218** — four hooks, two tree components, a context, an index. The packages
draw the line at **protocol and state**. The entire authoring experience — the accordion builder
(1,680), the CodeMirror DSL surface (1,251), the panes (1,103), the proposition explorer (607) — is
app-side and has never been forced to be otherwise.

**Where should the line be?**

| Candidate | Consequence |
|---|---|
| **Protocol & state** (status quo) | Packages stay small; every consumer rebuilds the authoring UI. A second app means rewriting ~5,000 lines |
| **Headless behaviour** | Packages own the *logic* of authoring — accordion state, insertion rules, DSL sync, completion sources — but render nothing. Consumers bring their own markup |
| **Headless + unstyled components** | Packages ship working, unstyled React components. Consumers theme them. Drags `@codemirror/*` into a package's peer dependencies |
| **Batteries included** | Packages ship a themed, complete authoring surface. Apps are composition, auth, and layout only |

Resolve using ticket 01's inventory — the classification of product-generic versus demo-specific is
the evidence, and this decision is where it gets spent.

The session must also answer:

1. **What does `@motiv/rules-react`'s 218 lines tell us?** Is it small because the boundary is right,
   or small because nothing ever pushed on it? Ticket 01's inventory should settle this empirically:
   how much of the demo is model-agnostic and reusable *as written*?
2. **The CodeMirror question.** The DSL surface is the most distinctive thing in the UI and the most
   expensive to rebuild. Promoting it makes `@codemirror/*` (six packages) a peer dependency. Is
   there a third option — a package that exports completion sources, lint diagnostics, and a
   highlight grammar *without* importing CodeMirror, leaving the editor binding to the consumer?
3. **Does the boundary differ by package?** `rules-core` is framework-free and could go deeper than
   `rules-react` without cost.

Blocks: 08 (new app or evolve the demo), 17 (non-React story).

## Answer

**Headless behaviour: the packages own the logic of authoring and render nothing.**

Evidence base: [demo-promotion-inventory.md](../research/demo-promotion-inventory.md) (ticket 01).

### The premise this ticket was written on was wrong

It assumed model coupling was the obstacle. Model coupling is **one constant** — `MODEL_TYPE` in
`App.tsx`, imported by five files, four of them in a single expression each. `Order`,
`loyalty-discount`, `can-checkout` and `fraud-screening` appear nowhere in `src/`. **85% of non-CSS
code is model-agnostic as written**, not "could be made agnostic".

So the boundary was never limited by what *can* move. It is limited by what the SDK wants to own as
public API.

### 1. The level — behaviour, not rendering

The codebase had already run the "promote a component" experiment and it failed.
`@motiv/rules-react` exports `RuleTree` — 42 lines, a pre-order flatten with ARIA `treeitem` wrappers
and a render prop. **It is imported by nothing in the demo.** The app wrote `RuleNodeEditor.tsx`, 292
lines of its own recursion, because it needed per-node collapse, pinning, popover arbitration,
insertion slots, and a caret whose meaning depends on whether the node has children.

The demand was never for markup — the demo was happy to render. It was for the behaviour underneath.

**Consequence: the styling blocker evaporates.** The inventory named unscoped class names against a
1,537-line global stylesheet as the thing that made components non-promotable, and called the fix *"a
decision with no precedent in this repo"*. Components are not promoted, so the decision does not
arise. The other blocker — CodeMirror — is handled in §3.

**`RuleTree` should be reconsidered.** It is the one export inconsistent with this boundary, and its
only consumer rejected it. → ticket 06 (API stability) owns whether it is deprecated or removed.

### 2. Scope — domain *and* workflow, in separate entry points

Two kinds of behaviour live in the promotable set:

- **Domain** — accordion/collapse state, insertion rules, path arithmetic, DSL sync, node summaries.
  About manipulating a rule document; identical in every conceivable consumer.
- **Workflow** — versioned optimistic save, 409 detection with a reload escape hatch, blast-radius
  reporting *before* the save, revert-vs-delete disambiguation read off the entry (the `DELETE`
  response cannot distinguish them), continuation guards so an in-flight answer never lands on a
  selection the user has moved off. `RuleHeader` (174) + `PropositionsPage` (400).

Workflow encodes **opinions**: that saves are optimistic, that conflicts are recoverable rather than
fatal, that a user sees blast radius before committing. An adopter may reject those and still want the
domain logic — so workflow ships behind its own entry point (a `/workflow` subpath), takeable or not.

574 lines of hard-won conflict handling is precisely what a second app would otherwise get subtly
wrong.

### 3. CodeMirror — neutral shapes, adapters app-side

The packages declare their own `CompletionItem` / `Diagnostic` / token-run types and export analysis
in their own vocabulary. **Zero CodeMirror coupling, including at the type level.** The CM adapter is
~20 lines in the consumer; Monaco or any other editor is equally served; both packages keep their
zero-runtime-dependency posture.

Viable because the demo already proves it: `builder/dslTokens.ts` (29 lines, no CM import) builds
highlighted token runs from core's `tokenize` and drives every collapsed builder row.

**`motivLanguage.ts` is reclassified as app-side** — it is a `StreamParser` plus `@lezer/highlight`
tags, CodeMirror constructs by definition. The inventory flagged it as the file most likely to be
reclassified here, and it is.

**Core exports its vocabulary.** `KEYWORDS` / `TYPES` / `QUANTIFIERS` are module-private today, so the
DSL vocabulary exists in **three copies** and `rules-core/src/dsl/lexer.ts:8-14` records that this has
already drifted once. Exporting them plus the token-run model reduces the remaining duplication from a
reimplementation to a thin grammar adapter.

### 4. Packaging

| goes to | what |
|---|---|
| `@motiv/rules-core` | everything framework-free: path arithmetic, insertion rules, the accordion state machine, DSL sync, completion, lint, token runs, vocabulary |
| `@motiv/rules-react` | React bindings — hooks holding state, effects, refs |
| `@motiv/rules-react/workflow` | workflow behaviour, separately importable |

Two packages, existing names, no new versioning surface. Maximising the framework-free surface is
deliberate: it is the only thing ticket 17's non-React story can ever build on.

### Excluded

- **`shell/`** (437 lines — `Modal`, `CommandPalette`, `Toolbar`, `useCommandKey`, `icons`). Renders,
  and imports nothing from either Motiv package. A `<dialog>` wrapper, a ⌘K palette and an SVG icon
  set are not rules domain, and shipping them would make `@motiv/rules-react` a general-purpose UI kit
  — a different product from its own description.
- **CodeMirror editor integration** (~1,058 lines): `DslEditor`, `useInlineDslEditor`, `motivLanguage`,
  `theme`, `payloadChips`, `hover`.
- **Styling** — moot, since components are not promoted.
- `App.tsx`, `main.tsx`, `CheckoutPane.tsx`, `RulesPage.tsx`, both stylesheets.

## Sequencing constraint from ticket 06

**Curate `rules-core`'s barrel before implementing this promotion.** `index.ts` is `export * from`
over 12 modules, so all ~3,400 promoted lines would publish every symbol they export the moment they
land — public API chosen by nobody, immediately before a compatibility policy freezes it. Curating
first makes each promoted export deliberate; curating after is a removal from a published surface.

Ticket 06 also **removes `RuleTree`** rather than deprecating it: this boundary made it inconsistent,
and its only consumer never imported it, so nothing depends on it.
