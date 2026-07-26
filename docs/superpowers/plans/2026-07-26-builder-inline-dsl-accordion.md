# Builder Inline-DSL Rows and Pinnable Accordion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the builder's single `expanded` flag into a structural caret (open by default, toggling a subtree between tree view and one editable line of DSL) and a per-node detail panel (closed by default, single-open, multi-open via pinning).

**Architecture:** Eight tasks. **1** adds `printInline` to the core DSL printer. **2** extracts the accordion into a pure, React-free state module. **3** rewires `RuleNodeEditor`/`BuilderPane` onto that module and moves the toolbars into the detail panel. **4** adds the pin control and close-all strip. **5** renders collapsed parents and all leaves as highlighted DSL text. **6** makes that text editable with a per-row CodeMirror. **7** scopes completion and removes the affordances DSL authoring replaces. **8** reworks the e2e suite and verifies everything.

**Tech Stack:** TypeScript, React 18, Vitest + @testing-library/react, Playwright, CodeMirror 6, vanilla CSS with design tokens. pnpm workspace.

## Global Constraints

- **Spec:** `docs/superpowers/specs/2026-07-26-builder-inline-dsl-accordion-design.md`.
- **TDD is mandatory** (CLAUDE.md): write the failing test, run it, confirm it fails for the right reason, then implement.
- **No backend change.** Nothing under `src/` (the .NET tree) is touched. No serialization, no API, no C#.
- **Structure default is open; detail default is closed.** Never reintroduce a seeded expanded set.
- **Round-trip invariant:** `parse(printInline(node)).document.rule` must deep-equal `node`. This is the correctness condition for editable rows — a row that renders one way and parses another corrupts the document on a no-op focus-and-blur.
- **Never squash whitespace on printed output.** `quote` uses `JSON.stringify`, which does not escape spaces, so a regex whitespace collapse silently rewrites a node named `"order  total"`.
- **Commit after every task.** Run the full package test suite before each commit.
- **After the final task**, spawn a `code-simplifier` agent over the changed files (CLAUDE.md requires this).

### Spec amendment applied by this plan

The spec's **Row Anatomy** puts `aria-expanded`/`aria-controls` on the row body, making it a `<button>`. In DSL view that same row body hosts a CodeMirror instance, and interactive content nested inside a button is invalid HTML that swallows events. The row body therefore carries **no** interactive role; a dedicated **details** button sits beside the pin. Task 3 updates the spec text to match.

## File Structure

**Create**
- `ui/packages/rules-core/test/dsl-printer-inline.test.ts` — `printInline` unit + round-trip tests.
- `ui/apps/demo/src/builder/accordion.ts` — pure accordion state model. No React import.
- `ui/apps/demo/test/builder/accordion.test.ts` — model unit tests.
- `ui/apps/demo/src/builder/NodeDsl.tsx` — one row's DSL surface: highlighted read state, CodeMirror edit state, parse-and-commit.
- `ui/apps/demo/src/builder/dslTokens.ts` — `tokenize` output → React spans.
- `ui/apps/demo/test/builder/NodeDsl.test.tsx` — read/edit/commit/revert tests.

**Modify**
- `ui/packages/rules-core/src/dsl/printer.ts` — thread a `Layout`, export `printInline`.
- `ui/apps/demo/src/builder/RuleNodeEditor.tsx` — row anatomy, two concerns, detail panel.
- `ui/apps/demo/src/panes/BuilderPane.tsx` — accordion model wiring, close-all strip.
- `ui/apps/demo/src/builder/NodeToolbar.tsx` — drop spec select and `+ quantifier`.
- `ui/apps/demo/src/dsl/completion.ts` — no change to signature; callers pass a scoped catalog.
- `ui/apps/demo/src/styles/app.css` — row, panel, pin, strip, DSL token colours.
- `docs/superpowers/specs/2026-07-26-builder-inline-dsl-accordion-design.md` — row anatomy amendment.
- Tests: `test/builder/RuleNodeEditor.test.tsx`, `test/builder/QuantifierNode.test.tsx`, `test/builder/ExtensionPoints.test.tsx`, `e2e/smoke.spec.ts`, `e2e/higher-order.spec.ts`, `e2e/dsl.spec.ts`, `e2e/live-rules.spec.ts`.

---

### Task 1: `printInline` in the core DSL printer

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/printer.ts`
- Test: `ui/packages/rules-core/test/dsl-printer-inline.test.ts` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `printInline(node: RuleNode): string`, exported from `@motiv/rules-core` (`dsl/index.ts` already does `export * from './printer.js'`).

Block-ness originates in exactly two places: `isMultiline` (which drives `broken` through `printChild` → `parenthesise`) and `printQuantifier`'s hardcoded `{\n…\n}`. A `Layout` parameter through the private functions is the whole change.

- [ ] **Step 1: Write the failing test**

Create `ui/packages/rules-core/test/dsl-printer-inline.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import { printInline } from '../src/dsl/printer.js';
import type { RuleNode } from '../src/document.js';

const NODES: Array<{ label: string; node: RuleNode; text: string }> = [
  { label: 'spec', node: { spec: 'is-active' }, text: 'is-active' },
  { label: 'negation', node: { not: { spec: 'is-flagged' } }, text: '!is-flagged' },
  {
    label: 'binary',
    node: { or: [{ spec: 'a' }, { not: { spec: 'b' } }] },
    text: 'a | !b',
  },
  {
    label: 'quantifier on one line',
    node: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: 2, path: 'orders' },
    text: 'atLeast(2) in orders { is-positive }',
  },
  {
    label: 'parameter count',
    node: { asAtLeastNSatisfied: { spec: 'is-positive' }, n: '@minOrders', path: 'orders' },
    text: 'atLeast(@minOrders) in orders { is-positive }',
  },
  {
    label: 'quantifier under an operator stays on one line',
    node: {
      and: [{ spec: 'is-active' }, { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' }],
    },
    text: 'is-active & all in orders { is-positive }',
  },
  {
    label: 'looser child keeps its parentheses',
    node: { and: [{ orElse: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] },
    text: '(a || b) & c',
  },
  {
    label: 'named compound',
    node: { andAlso: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' },
    text: '(a && b) as "pair"',
  },
];

describe('printInline', () => {
  it.each(NODES)('renders $label on a single line', ({ node, text }) => {
    const printed = printInline(node);
    expect(printed).toBe(text);
    expect(printed).not.toContain('\n');
  });

  it.each(NODES)('round-trips $label through the parser', ({ node }) => {
    const result = parse(printInline(node));
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual(node);
  });

  it('preserves consecutive spaces inside a name', () => {
    const node: RuleNode = { spec: 'is-active', name: 'order  total' };
    expect(printInline(node)).toBe('is-active as "order  total"');
    expect(parse(printInline(node)).document?.rule).toEqual(node);
  });
});
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `cd ui/packages/rules-core && pnpm vitest run test/dsl-printer-inline.test.ts`

Expected: FAIL — `printInline` is not exported from `../src/dsl/printer.js`.

- [ ] **Step 3: Thread a `Layout` through the printer**

In `ui/packages/rules-core/src/dsl/printer.ts`, add the type below `const ATOM = PRECEDENCE.length;`:

```ts
/**
 * How a node is laid out. `'block'` breaks quantifiers and the groups containing them across
 * lines; `'inline'` keeps everything on one, for rendering a node inside a single-line row.
 */
type Layout = 'block' | 'inline';
```

Change `isMultiline` to consult it — under `'inline'` nothing is ever multi-line, which is what stops `parenthesise` breaking groups:

```ts
function isMultiline(node: RuleNode, layout: Layout): boolean {
  if (layout === 'inline') return false;
  if (isHigherOrderNode(node)) return true;
  if (isNotNode(node)) return isMultiline(node.not, layout);
  if (isBinaryNode(node)) return operandsOf(node).some((operand) => isMultiline(operand, layout));
  return false;
}
```

Thread `layout` through the remaining private functions:

```ts
function printChild(node: RuleNode, indent: string, needsParens: boolean, broken: boolean, layout: Layout): string {
  if (!needsParens) return printNode(node, indent, layout);
  return parenthesise(indent, broken, (inner) => printNode(node, inner, layout));
}

function printQuantifier(node: HigherOrderNode, indent: string, layout: Layout): string {
  const count = 'n' in node ? `(${String(node.n)})` : '';
  const head = `${QUANTIFIER_WORDS[higherOrderKey(node)]}${count} in ${node.path}`;
  if (layout === 'inline') return `${head} { ${printNode(higherOrderBody(node), indent, layout)} }`;
  const inner = indent + INDENT;
  return `${head} {\n${inner}${printNode(higherOrderBody(node), inner, layout)}\n${indent}}`;
}

function printNegation(node: NotNode, indent: string, layout: Layout): string {
  const operand = node.not;
  return `!${printChild(operand, indent, precedenceOf(operand) < ATOM, isMultiline(operand, layout), layout)}`;
}

function printBinary(node: BinaryNode, indent: string, layout: Layout): string {
  const operator = binaryOperator(node);
  const broken = isMultiline(node, layout);
  const parts = operandsOf(node).map((operand) =>
    printChild(operand, indent, operandNeedsParens(operand, operator), broken, layout));
  return parts.join(` ${OPERATOR_TEXT[operator]} `);
}

function printBody(node: RuleNode, indent: string, layout: Layout): string {
  if (isSpecNode(node)) return node.spec;
  if (isExpressionNode(node)) return `\`${node.expression}\``;
  if (isNotNode(node)) return printNegation(node, indent, layout);
  if (isHigherOrderNode(node)) return printQuantifier(node, indent, layout);
  return printBinary(node, indent, layout);
}

function printNode(node: RuleNode, indent: string, layout: Layout): string {
  const name = node.name;
  if (name === undefined) return printBody(node, indent, layout);
  if (!nameNeedsParens(node)) return `${printBody(node, indent, layout)} as ${quote(name)}`;

  const group = parenthesise(indent, isMultiline(node, layout), (inner) => printBody(node, inner, layout));
  return `${group} as ${quote(name)}`;
}
```

- [ ] **Step 4: Add the two public entry points**

Replace the existing `print` with:

```ts
/** Reprints a rule document as canonical DSL text — the inverse of `parse`. */
export function print(document: RuleDocument): string {
  return `${printParameters(document.parameters)}${printNode(document.rule, '', 'block')}`;
}

/**
 * Renders a single node as one line of DSL, for showing it inside a row. Quantifier bodies are
 * braced on the same line rather than broken across several.
 *
 * `parse(printInline(node)).document.rule` deep-equals `node`, which is what makes a rendered
 * row safe to hand back to the parser after editing.
 */
export function printInline(node: RuleNode): string {
  return printNode(node, '', 'inline');
}
```

- [ ] **Step 5: Run the new test and the existing printer/round-trip suites**

Run: `cd ui/packages/rules-core && pnpm vitest run`

Expected: PASS. `dsl-printer.test.ts` and `dsl-roundtrip.test.ts` must be unchanged and green — `print` still passes `'block'`, so block output is byte-identical.

- [ ] **Step 6: Extend the shared round-trip corpus**

Append to `ui/packages/rules-core/test/dsl-roundtrip.test.ts`, inside `describe('DSL round-trip', …)`:

```ts
  it.each(DOCUMENTS)('parse(printInline(rule)) preserves $label', ({ document }) => {
    const result = parse(printInline(document.rule));
    expect(result.errors).toEqual([]);
    expect(result.document?.rule).toEqual(document.rule);
  });
```

Add `printInline` to the existing printer import at the top of that file:

```ts
import { print, printInline } from '../src/dsl/printer.js';
```

- [ ] **Step 7: Run the full core suite**

Run: `cd ui/packages/rules-core && pnpm vitest run && pnpm typecheck`

Expected: PASS. The `reference composition` document in that corpus is the mockup's rule, so this exercises the real target shape.

- [ ] **Step 8: Commit**

```bash
git add ui/packages/rules-core/src/dsl/printer.ts ui/packages/rules-core/test/dsl-printer-inline.test.ts ui/packages/rules-core/test/dsl-roundtrip.test.ts
git commit -m "feat(rules-core): render a rule node as one line of DSL"
```

---

### Task 2: The accordion state model

**Files:**
- Create: `ui/apps/demo/src/builder/accordion.ts`
- Test: `ui/apps/demo/test/builder/accordion.test.ts` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `AccordionModel`, `EMPTY_ACCORDION`, `isCollapsed(model, path)`, `isOpen(model, path)`, `isPinned(model, path)`, `toggleCollapsed(model, path)`, `toggleOpen(model, path)`, `togglePin(model, path)`, `closeAll(model)` — all `(AccordionModel, string) => AccordionModel` or `=> boolean`.

Kept React-free so the displacement rules can be tested without rendering anything. Task 3 consumes it.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/builder/accordion.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import {
  EMPTY_ACCORDION, closeAll, isCollapsed, isOpen, isPinned,
  toggleCollapsed, toggleOpen, togglePin,
} from '../../src/builder/accordion.js';

const A = '$.rule.and[0]';
const B = '$.rule.and[1]';

describe('accordion structure', () => {
  it('starts with every subtree expanded', () => {
    expect(isCollapsed(EMPTY_ACCORDION, A)).toBe(false);
  });

  it('collapses and re-expands a subtree', () => {
    const collapsed = toggleCollapsed(EMPTY_ACCORDION, A);
    expect(isCollapsed(collapsed, A)).toBe(true);
    expect(isCollapsed(toggleCollapsed(collapsed, A), A)).toBe(false);
  });

  it('does not touch detail state', () => {
    const model = toggleCollapsed(toggleOpen(EMPTY_ACCORDION, A), A);
    expect(isCollapsed(model, A)).toBe(true);
    expect(isOpen(model, A)).toBe(true);
  });
});

describe('accordion detail', () => {
  it('starts with every panel closed', () => {
    expect(isOpen(EMPTY_ACCORDION, A)).toBe(false);
  });

  it('opening one panel displaces the previous transient', () => {
    const model = toggleOpen(toggleOpen(EMPTY_ACCORDION, A), B);
    expect(isOpen(model, A)).toBe(false);
    expect(isOpen(model, B)).toBe(true);
  });

  it('closes a panel when its own row is toggled again', () => {
    const model = toggleOpen(toggleOpen(EMPTY_ACCORDION, A), A);
    expect(isOpen(model, A)).toBe(false);
  });

  it('a pinned panel survives another opening', () => {
    const model = toggleOpen(togglePin(toggleOpen(EMPTY_ACCORDION, A), A), B);
    expect(isPinned(model, A)).toBe(true);
    expect(isOpen(model, A)).toBe(true);
    expect(isOpen(model, B)).toBe(true);
  });

  it('pinning frees the transient slot', () => {
    const model = togglePin(toggleOpen(EMPTY_ACCORDION, A), A);
    expect(model.openPath).toBeNull();
  });

  it('unpinning keeps the panel open, as the transient', () => {
    const pinned = togglePin(toggleOpen(EMPTY_ACCORDION, A), A);
    const model = togglePin(pinned, A);
    expect(isPinned(model, A)).toBe(false);
    expect(isOpen(model, A)).toBe(true);
    expect(model.openPath).toBe(A);
  });

  it('closing a pinned panel unpins it — there is no pinned-but-closed state', () => {
    const pinned = togglePin(toggleOpen(EMPTY_ACCORDION, A), A);
    const model = toggleOpen(pinned, A);
    expect(isPinned(model, A)).toBe(false);
    expect(isOpen(model, A)).toBe(false);
  });

  it('close all clears the transient and every pin, leaving structure alone', () => {
    const collapsed = toggleCollapsed(EMPTY_ACCORDION, B);
    const model = closeAll(toggleOpen(togglePin(toggleOpen(collapsed, A), A), B));
    expect(model.openPath).toBeNull();
    expect(model.pinned.size).toBe(0);
    expect(isCollapsed(model, B)).toBe(true);
  });
});
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/accordion.test.ts`

Expected: FAIL — cannot resolve `../../src/builder/accordion.js`.

- [ ] **Step 3: Write the model**

Create `ui/apps/demo/src/builder/accordion.ts`:

```ts
/**
 * The builder's two independent view concerns, which an earlier revision conflated into one
 * `expanded` set. They want opposite defaults — structure open, detail closed — so no single
 * flag can serve both.
 *
 * `collapsed` is structural: which subtrees are folded into a single line of DSL.
 * `openPath` + `pinned` are the detail accordion: at most one *transient* panel, plus any
 * number of pinned ones that opening another node does not displace.
 *
 * Paths are keys, and they shift when an operand is removed (`$.rule.and[1]` becomes
 * `$.rule.and[0]`). Stale entries address nodes that no longer exist and are inert, matching
 * how the set this replaces already behaved. Nothing prunes them.
 */
export interface AccordionModel {
  readonly collapsed: ReadonlySet<string>;
  readonly openPath: string | null;
  readonly pinned: ReadonlySet<string>;
}

/** Every subtree expanded, every detail panel closed, nothing pinned. */
export const EMPTY_ACCORDION: AccordionModel = {
  collapsed: new Set(),
  openPath: null,
  pinned: new Set(),
};

function added(set: ReadonlySet<string>, value: string): ReadonlySet<string> {
  return new Set(set).add(value);
}

function removed(set: ReadonlySet<string>, value: string): ReadonlySet<string> {
  const next = new Set(set);
  next.delete(value);
  return next;
}

export function isCollapsed(model: AccordionModel, path: string): boolean {
  return model.collapsed.has(path);
}

export function isPinned(model: AccordionModel, path: string): boolean {
  return model.pinned.has(path);
}

/** A panel is open when it is the transient one or has been pinned open. */
export function isOpen(model: AccordionModel, path: string): boolean {
  return model.openPath === path || model.pinned.has(path);
}

/** Folds a subtree into DSL text, or unfolds it. Never touches detail state. */
export function toggleCollapsed(model: AccordionModel, path: string): AccordionModel {
  const collapsed = isCollapsed(model, path)
    ? removed(model.collapsed, path)
    : added(model.collapsed, path);
  return { ...model, collapsed };
}

/**
 * Opens a node's detail panel, displacing the previous transient — but never a pinned panel.
 * Toggling a pinned node closes *and* unpins it, so a panel is never pinned-but-closed.
 */
export function toggleOpen(model: AccordionModel, path: string): AccordionModel {
  if (model.pinned.has(path)) {
    return {
      ...model,
      pinned: removed(model.pinned, path),
      openPath: model.openPath === path ? null : model.openPath,
    };
  }
  return { ...model, openPath: model.openPath === path ? null : path };
}

/**
 * Pinning moves a panel out of the transient slot, so the next node opened does not displace it.
 * Unpinning hands it back to that slot rather than closing it — clicking a pin should never make
 * content vanish.
 */
export function togglePin(model: AccordionModel, path: string): AccordionModel {
  if (model.pinned.has(path)) {
    return { ...model, pinned: removed(model.pinned, path), openPath: path };
  }
  return {
    ...model,
    pinned: added(model.pinned, path),
    openPath: model.openPath === path ? null : model.openPath,
  };
}

/** Closes every detail panel, pinned or not. Structure is left as it is. */
export function closeAll(model: AccordionModel): AccordionModel {
  return { ...model, openPath: null, pinned: new Set() };
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/accordion.test.ts`

Expected: PASS, 12 tests.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/accordion.ts ui/apps/demo/test/builder/accordion.test.ts
git commit -m "feat(demo): model the builder accordion as pure state"
```

---

### Task 3: Split structure from detail in the builder

**Files:**
- Modify: `ui/apps/demo/src/builder/RuleNodeEditor.tsx`
- Modify: `ui/apps/demo/src/panes/BuilderPane.tsx`
- Modify: `ui/apps/demo/src/styles/app.css:376-464`
- Modify: `docs/superpowers/specs/2026-07-26-builder-inline-dsl-accordion-design.md`
- Test: `ui/apps/demo/test/builder/RuleNodeEditor.test.tsx`, `test/builder/QuantifierNode.test.tsx`, `test/builder/ExtensionPoints.test.tsx`

**Interfaces:**
- Consumes: `AccordionModel`, `EMPTY_ACCORDION`, `isCollapsed`, `isOpen`, `isPinned`, `toggleCollapsed`, `toggleOpen`, `togglePin` from Task 2.
- Produces: `AccordionContext` carrying `{ model, toggleCollapsed(path), toggleOpen(path), togglePin(path), closeAll(), catalog }`; the aria-labels `collapse {path}` / `expand {path}` (caret) and `details for {path}` (detail toggle); the panel id `detail-{path}`.

The detail panel now holds `DecorationEditor` **plus** `NodeToolbar`/`QuantifierNode`. Every existing test that drives a toolbar control must first open the node.

- [ ] **Step 1: Write the failing tests**

Replace the body of `describe('BuilderPane accordion (boolean)', …)` in `ui/apps/demo/test/builder/RuleNodeEditor.test.tsx` with the following (keep the imports and `catalog`/`client`/`renderWith` helpers above it exactly as they are):

```tsx
describe('BuilderPane accordion (boolean)', () => {
  const openDetail = async (path: string) => {
    fireEvent.click(await screen.findByRole('button', { name: `details for ${path}` }));
  };

  it('starts with every detail panel closed', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByRole('button', { name: 'details for $.rule' });
    expect(screen.queryByLabelText('name at $.rule')).toBeNull();
    expect(screen.queryByRole('button', { name: 'toggle NOT at $.rule' })).toBeNull();
  });

  it('starts with every subtree expanded', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    expect(await screen.findByRole('button', { name: 'details for $.rule.and[1]' })).toBeDefined();
  });

  it('wraps a leaf in AND and shows two operands', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'wrap $.rule in AND' }));
    const rule = store.getState().document.rule as { and?: unknown[] };
    expect(rule.and).toHaveLength(2);
  });

  it('toggles NOT on a leaf and back', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'toggle NOT at $.rule' }));
    expect(store.getState().document.rule).toEqual({ not: { spec: 'is-active' } });
    fireEvent.click(screen.getByRole('button', { name: 'toggle NOT at $.rule' }));
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
  });

  it('edits whenTrue decoration into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    expect((store.getState().document.rule as { whenTrue?: string }).whenTrue).toBe('yes');
  });

  it('opening one panel closes the previously open one', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule.and[0]');
    expect(screen.getByLabelText('name at $.rule.and[0]')).toBeDefined();
    await openDetail('$.rule.and[1]');
    expect(screen.queryByLabelText('name at $.rule.and[0]')).toBeNull();
    expect(screen.getByLabelText('name at $.rule.and[1]')).toBeDefined();
  });

  it('collapsing a subtree hides its children but not its detail panel', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule' }));
    expect(screen.queryByRole('button', { name: 'details for $.rule.and[1]' })).toBeNull();
    expect(screen.getByLabelText('name at $.rule')).toBeDefined();
  });

  it('re-expanding a child does not collapse the root subtree', async () => {
    const store = new RuleEditorStore({ rule: { and: [ { or: [ { spec: 'is-active' }, { spec: 'is-adult' } ] }, { spec: 'is-active' } ] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'details for $.rule.and[1]' });
    fireEvent.click(screen.getByRole('button', { name: 'collapse $.rule.and[0]' }));
    fireEvent.click(screen.getByRole('button', { name: 'expand $.rule.and[0]' }));
    expect(screen.getByRole('button', { name: 'details for $.rule.and[1]' })).toBeDefined();
  });

  it('offers remove only on operand elements, not on a NOT child', async () => {
    const store = new RuleEditorStore({ rule: { not: { spec: 'is-active' } } });
    renderWith(store);
    await openDetail('$.rule.not');
    expect(screen.queryByRole('button', { name: 'remove $.rule.not' })).toBeNull();
  });
});
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/RuleNodeEditor.test.tsx`

Expected: FAIL — no `details for $.rule` button exists yet.

- [ ] **Step 3: Rewrite `RuleNodeEditor`**

Replace `ui/apps/demo/src/builder/RuleNodeEditor.tsx` with:

```tsx
import { createContext, useContext } from 'react';
import { isHigherOrderNode, type Catalog } from '@motiv/rules-core';
import { useRuleNode } from '@motiv/rules-react';
import { NodeToolbar } from './NodeToolbar.js';
import { QuantifierNode } from './QuantifierNode.js';
import { DecorationEditor } from './DecorationEditor.js';
import { childPaths } from './childPaths.js';
import { summarize } from './nodeSummary.js';
import { isCollapsed, isOpen, isPinned, type AccordionModel } from './accordion.js';

/** The accordion state and its transitions, shared by every {@link RuleNodeEditor} in the tree. */
export interface AccordionState {
  model: AccordionModel;
  toggleCollapsed: (path: string) => void;
  toggleOpen: (path: string) => void;
  togglePin: (path: string) => void;
  closeAll: () => void;
  catalog: Catalog;
}

export const AccordionContext = createContext<AccordionState | null>(null);

export function useAccordion(): AccordionState {
  const context = useContext(AccordionContext);
  if (!context) throw new Error('RuleNodeEditor must be used within an AccordionContext provider.');
  return context;
}

/** The id tying a node's detail toggle to the panel it opens. */
const panelId = (path: string): string => `detail-${path}`;

/**
 * Recursively renders a rule node.
 *
 * Two view concerns, deliberately independent. The **caret** is structural: it folds a subtree
 * into a single line of DSL and back, and starts expanded. The **detail** panel holds the node's
 * decoration fields and edit controls, starts closed, and is displaced when another node is
 * opened unless it has been pinned. A node can be collapsed with its panel open, or the reverse.
 *
 * The row body carries no interactive role of its own. It has to host a text editor once the
 * subtree is collapsed, and interactive content nested inside a button is invalid HTML that
 * swallows events — so the detail toggle is a sibling control rather than the row itself.
 */
export function RuleNodeEditor(props: { path: string; modelType: string }) {
  const { path, modelType } = props;
  const { node, errors } = useRuleNode(path);
  const { model, toggleCollapsed, toggleOpen, togglePin, catalog } = useAccordion();

  if (!node) return null;

  const kids = childPaths(node, path);
  const hasChildren = kids.length > 0;
  const collapsed = isCollapsed(model, path);
  const open = isOpen(model, path);
  const pinned = isPinned(model, path);
  const summary = summarize(node);

  // A quantifier's single child is scoped to the collection's element model type, not the parent's.
  const childModelType = isHigherOrderNode(node)
    ? (catalog.collections.find((c) => c.path === node.path)?.elementModelType ?? modelType)
    : modelType;

  return (
    <div className="node">
      <div className="node-row">
        {hasChildren ? (
          <button
            type="button"
            className="node-chev"
            aria-expanded={!collapsed}
            aria-label={`${collapsed ? 'expand' : 'collapse'} ${path}`}
            onClick={() => toggleCollapsed(path)}
          >
            {collapsed ? '▸' : '▾'}
          </button>
        ) : (
          <span className="node-bullet" aria-hidden="true">•</span>
        )}
        <span className="node-body">
          <span className={`node-badge node-badge-${summary.kind}`}>{summary.badge}</span>
          {summary.description && <span className="node-desc">{summary.description}</span>}
          {node.name && <span className="node-name">as &quot;{node.name}&quot;</span>}
        </span>
        <button
          type="button"
          className={open ? 'node-detail-toggle open' : 'node-detail-toggle'}
          aria-expanded={open}
          aria-controls={panelId(path)}
          aria-label={`details for ${path}`}
          onClick={() => toggleOpen(path)}
        >
          ⋯
        </button>
        <button
          type="button"
          className={pinned ? 'node-pin pinned' : 'node-pin'}
          aria-pressed={pinned}
          aria-label={`${pinned ? 'unpin' : 'pin'} ${path}`}
          onClick={() => togglePin(path)}
        >
          📌
        </button>
      </div>
      {errors.length > 0 && (
        <span role="alert" className="error">{errors.map((e) => e.message).join('; ')}</span>
      )}
      {open && (
        <div className="node-detail" id={panelId(path)}>
          {isHigherOrderNode(node) ? (
            <QuantifierNode path={path} node={node} catalog={catalog} modelType={modelType} />
          ) : (
            <NodeToolbar path={path} node={node} modelType={modelType} catalog={catalog} />
          )}
          <DecorationEditor path={path} node={node} />
        </div>
      )}
      {hasChildren && !collapsed && (
        <div className="node-kids">
          {kids.map((childPath) => (
            <RuleNodeEditor key={childPath} path={childPath} modelType={childModelType} />
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Rewire `BuilderPane`**

Replace `ui/apps/demo/src/panes/BuilderPane.tsx` with:

```tsx
import { useState } from 'react';
import type { Catalog, RulesApiClient } from '@motiv/rules-core';
import { useCatalog } from '@motiv/rules-react';
import { AccordionContext, RuleNodeEditor } from '../builder/RuleNodeEditor.js';
import {
  EMPTY_ACCORDION, closeAll, toggleCollapsed, toggleOpen, togglePin,
  type AccordionModel,
} from '../builder/accordion.js';
import { MODEL_TYPE } from '../App.js';

const ROOT = '$.rule';
/** What a pane renders against until (or unless) the real catalog arrives. */
export const EMPTY_CATALOG: Catalog = { specs: [], collections: [] };

/**
 * The recursive rule builder over the boolean grammar, without any surrounding pane chrome — so
 * it can be hosted either by {@link BuilderPane} or as one surface of a pane that toggles between
 * the builder and the DSL text editor.
 *
 * Accordion state is demo-local UI state, not document state, and is held here so that both the
 * tree and the close-all strip read the one model.
 */
export function BuilderBody(props: { client: RulesApiClient }) {
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : EMPTY_CATALOG;

  const [model, setModel] = useState<AccordionModel>(EMPTY_ACCORDION);

  return (
    <>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      {catalogState.status === 'error' && <p role="alert">Failed to load catalog.</p>}
      <AccordionContext.Provider
        value={{
          model,
          toggleCollapsed: (path) => setModel((prev) => toggleCollapsed(prev, path)),
          toggleOpen: (path) => setModel((prev) => toggleOpen(prev, path)),
          togglePin: (path) => setModel((prev) => togglePin(prev, path)),
          closeAll: () => setModel(closeAll),
          catalog,
        }}
      >
        <RuleNodeEditor path={ROOT} modelType={MODEL_TYPE} />
      </AccordionContext.Provider>
    </>
  );
}

/** The builder as a standalone pane, for hosts that show it without the DSL surface. */
export function BuilderPane(props: { client: RulesApiClient }) {
  return (
    <section className="pane" aria-label="Builder">
      <div className="pane-header">
        <h2>Builder</h2>
      </div>
      <BuilderBody client={props.client} />
    </section>
  );
}
```

Note the removed imports: `listPaths`, `splitLast`, `RuleDocument`, `useRuleEditorStore`, and the `MAX_EXPAND_DEPTH`/`depthOf`/`parentPrefixOf`/`initialExpanded` helpers all go with the seeded expanded set.

- [ ] **Step 5: Update the CSS**

In `ui/apps/demo/src/styles/app.css`, replace the block comment at lines 376-387 and the `.node-toolbar, .node-detail` rule with:

```css
/*
  Builder: a rule node's row. Two independent view concerns meet here — the caret folds the
  subtree (structure, open by default), and the `⋯` toggle opens the detail panel (metadata and
  edit controls, closed by default, single-open unless pinned). A collapsed node therefore costs
  exactly one compact row rather than a full bordered card.

  Indentation is structural rather than computed: a level's rows sit inside their parent's
  `.node-kids`, whose left margin is the one indent step. Multiplying a `--depth` custom property
  on top of that (as an earlier revision did) double-counts the nesting and fans the tree out
  further at every level. `--depth` still earns its keep on `.assertion`, which renders the
  justification tree as a flat list with no nesting to read the level from.
*/
```

Then add, after the existing `.node-name` rule:

```css
/* The badge/gloss/name group, or — once collapsed — the node's DSL text. Never interactive:
   in DSL view it hosts a text editor, which cannot live inside a button. */
.node-body {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1 1 auto;
  min-width: 0;
}

.node-bullet {
  width: 14px;
  flex: none;
  font-size: 10px;
  color: var(--faint);
  text-align: center;
}

/* Row controls stay quiet until the row is hovered or the control is active, so a resting tree
   reads as structure rather than as a grid of buttons. Focus must still reveal them, or they
   become keyboard-inaccessible. */
.node-detail-toggle,
.node-pin {
  appearance: none;
  border: none;
  background: transparent;
  flex: none;
  padding: 0 4px;
  font-size: 11px;
  line-height: 1;
  color: var(--muted);
  cursor: pointer;
  opacity: 0;
}

.node-row:hover .node-detail-toggle,
.node-row:hover .node-pin,
.node-detail-toggle:focus-visible,
.node-pin:focus-visible,
.node-detail-toggle.open,
.node-pin.pinned {
  opacity: 1;
}

.node-pin.pinned { color: var(--accent); }
```

Replace the combined `.node-toolbar, .node-detail` rule with separate rules, since the toolbar now sits *inside* the panel rather than beside it:

```css
.node-detail {
  display: flex;
  flex-direction: column;
  gap: var(--space);
  margin: 0 0 8px 10px;
  padding: 9px 10px;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
}

.node-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: calc(var(--space) / 2);
}

.node-kids {
  display: flex;
  flex-direction: column;
  margin-left: 10px;
}
```

- [ ] **Step 6: Update the other two builder test files**

In `ui/apps/demo/test/builder/QuantifierNode.test.tsx`, every test that reads a `quantifier kind at …` / `quantifier collection at …` / `quantifier n at …` control must first click `details for {path}`. Replace the existing `collapse $.rule` click at line 21 with an open of the detail panel:

```tsx
fireEvent.click(await screen.findByRole('button', { name: 'details for $.rule' }));
```

In `ui/apps/demo/test/builder/ExtensionPoints.test.tsx`, do the same for any assertion on the disabled `expression — coming` button, which now lives inside the panel.

- [ ] **Step 7: Run the demo suite**

Run: `cd ui/apps/demo && pnpm vitest run && pnpm typecheck`

Expected: PASS. If a test still fails on a missing toolbar control, it is missing its `details for …` click.

- [ ] **Step 8: Amend the spec's Row Anatomy**

In `docs/superpowers/specs/2026-07-26-builder-inline-dsl-accordion-design.md`, replace the **Row Anatomy** section's control list with:

```markdown
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
```

- [ ] **Step 9: Commit**

```bash
git add ui/apps/demo/src ui/apps/demo/test ui/apps/demo/src/styles/app.css docs/superpowers/specs/2026-07-26-builder-inline-dsl-accordion-design.md
git commit -m "feat(demo): separate the builder's structure and detail concerns"
```

---

### Task 4: The close-all strip

**Files:**
- Modify: `ui/apps/demo/src/panes/BuilderPane.tsx`
- Modify: `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/builder/accordion-strip.test.tsx` (create)

**Interfaces:**
- Consumes: `AccordionModel.pinned`, `closeAll` from Task 2; `BuilderBody` from Task 3.
- Produces: a `close all` button, and the text `{n} pinned`.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/builder/accordion-strip.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [{ name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null }],
  collections: [],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('close-all strip', () => {
  const doc = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-active' }] } };

  it('stays hidden while nothing is pinned', async () => {
    renderWith(new RuleEditorStore(doc));
    await screen.findByRole('button', { name: 'details for $.rule' });
    expect(screen.queryByRole('button', { name: 'close all' })).toBeNull();
  });

  it('appears once a node is pinned, and counts the pins', async () => {
    renderWith(new RuleEditorStore(doc));
    fireEvent.click(await screen.findByRole('button', { name: 'pin $.rule.and[0]' }));
    expect(screen.getByRole('button', { name: 'close all' })).toBeDefined();
    expect(screen.getByText('1 pinned')).toBeDefined();
    fireEvent.click(screen.getByRole('button', { name: 'pin $.rule.and[1]' }));
    expect(screen.getByText('2 pinned')).toBeDefined();
  });

  it('close all clears every pin and the open panel', async () => {
    renderWith(new RuleEditorStore(doc));
    fireEvent.click(await screen.findByRole('button', { name: 'pin $.rule.and[0]' }));
    fireEvent.click(screen.getByRole('button', { name: 'details for $.rule.and[1]' }));
    fireEvent.click(screen.getByRole('button', { name: 'close all' }));
    expect(screen.queryByLabelText('name at $.rule.and[0]')).toBeNull();
    expect(screen.queryByLabelText('name at $.rule.and[1]')).toBeNull();
    expect(screen.queryByRole('button', { name: 'close all' })).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/accordion-strip.test.tsx`

Expected: FAIL — no `close all` button.

- [ ] **Step 3: Render the strip**

In `ui/apps/demo/src/panes/BuilderPane.tsx`, insert immediately before the `<AccordionContext.Provider>`:

```tsx
      {/* Height is reserved rather than conditional, so the tree does not jump when the first
          node is pinned. */}
      <div className="accordion-strip">
        {model.pinned.size > 0 && (
          <>
            <span className="caption">{model.pinned.size} pinned</span>
            <button type="button" className="btn" onClick={() => setModel(closeAll)}>
              close all
            </button>
          </>
        )}
      </div>
```

- [ ] **Step 4: Style it**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* Reserved-height strip above the tree: it holds the pin count and close-all once anything is
   pinned, and stays empty (but present) otherwise so the tree never shifts underneath. */
.accordion-strip {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: calc(var(--space) / 2);
  min-height: 26px;
  padding: 0 2px;
}
```

- [ ] **Step 5: Run the test and confirm it passes**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/accordion-strip.test.tsx`

Expected: PASS, 3 tests.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/src/panes/BuilderPane.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/accordion-strip.test.tsx
git commit -m "feat(demo): add the builder's pin count and close-all strip"
```

---

### Task 5: Render collapsed subtrees and leaves as DSL text

**Files:**
- Create: `ui/apps/demo/src/builder/dslTokens.ts`
- Create: `ui/apps/demo/src/builder/NodeDsl.tsx`
- Modify: `ui/apps/demo/src/builder/RuleNodeEditor.tsx`
- Modify: `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/builder/NodeDsl.test.tsx` (create)

**Interfaces:**
- Consumes: `printInline` (Task 1); `isCollapsed` (Task 2); `tokenize`, `Token`, `TokenKind` from `@motiv/rules-core`.
- Produces: `<NodeDsl path node modelType catalog />`; `tokenSpans(text: string): Array<{ key: string; kind: TokenKind; value: string }>`; the aria-label `expression at {path}`.

A leaf is permanently in DSL view — `printBody` renders a spec node as its bare name, so its tree form and text form are the same string and there is nothing to toggle. That is what keeps DSL view universal, which the next task's authoring story depends on.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/builder/NodeDsl.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
    { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('DSL rows', () => {
  it('renders a leaf as its bare spec name', async () => {
    renderWith(new RuleEditorStore({ rule: { spec: 'is-active' } }));
    const row = await screen.findByLabelText('expression at $.rule');
    expect(row.textContent).toBe('is-active');
  });

  it('renders a collapsed subtree as one line of DSL', async () => {
    const store = new RuleEditorStore({
      rule: { or: [{ spec: 'is-active' }, { not: { spec: 'is-adult' } }] },
    });
    renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.getByLabelText('expression at $.rule').textContent).toBe('is-active | !is-adult');
  });

  it('renders a collapsed quantifier body on the same line', async () => {
    const store = new RuleEditorStore({
      rule: { asAtLeastNSatisfied: { spec: 'is-active' }, n: 2, path: 'orders' },
    });
    renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    expect(screen.getByLabelText('expression at $.rule').textContent)
      .toBe('atLeast(2) in orders { is-active }');
  });

  it('shows the badge and gloss while expanded, not the DSL', async () => {
    const store = new RuleEditorStore({ rule: { or: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await screen.findByRole('button', { name: 'collapse $.rule' });
    expect(screen.queryByLabelText('expression at $.rule')).toBeNull();
    expect(screen.getByText('any may hold')).toBeDefined();
  });

  it('classifies tokens so they can be coloured', async () => {
    renderWith(new RuleEditorStore({ rule: { not: { spec: 'is-active' } } }));
    const store = await screen.findByLabelText('expression at $.rule.not');
    expect(store.querySelector('.tok-spec')).not.toBeNull();
  });
});
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/NodeDsl.test.tsx`

Expected: FAIL — no `expression at $.rule` label.

- [ ] **Step 3: Write the token splitter**

Create `ui/apps/demo/src/builder/dslTokens.ts`:

```ts
import { tokenize, type TokenKind } from '@motiv/rules-core';

/** One rendered run of DSL text: a lexed token, or the gap of whitespace before it. */
export interface TokenSpan {
  key: string;
  kind: TokenKind | 'gap';
  value: string;
}

/**
 * Splits DSL text into renderable runs. The lexer skips whitespace, so the gaps between tokens
 * are re-inserted verbatim — a row that dropped them would render `a&b` for `a & b`, and the
 * text is the node's only visible description once its subtree is collapsed.
 */
export function tokenSpans(text: string): TokenSpan[] {
  const spans: TokenSpan[] = [];
  let cursor = 0;
  for (const token of tokenize(text)) {
    if (token.from > cursor) {
      spans.push({ key: `gap-${cursor}`, kind: 'gap', value: text.slice(cursor, token.from) });
    }
    spans.push({ key: `${token.kind}-${token.from}`, kind: token.kind, value: token.value });
    cursor = token.to;
  }
  if (cursor < text.length) {
    spans.push({ key: `gap-${cursor}`, kind: 'gap', value: text.slice(cursor) });
  }
  return spans;
}
```

- [ ] **Step 4: Write the read-state row**

Create `ui/apps/demo/src/builder/NodeDsl.tsx`:

```tsx
import { printInline, type Catalog, type RuleNode } from '@motiv/rules-core';
import { tokenSpans } from './dslTokens.js';

/**
 * A node rendered as one line of DSL — what a leaf always shows, and what a parent shows once
 * its subtree is collapsed.
 *
 * The text is `printInline`'s output, which the parser accepts verbatim, so the row is safe to
 * hand back after an edit. It is truncated with an ellipsis rather than wrapped, so a long
 * expression cannot push the row's controls out of reach.
 */
export function NodeDsl(props: { path: string; node: RuleNode; modelType: string; catalog: Catalog }) {
  const { path, node } = props;
  const text = printInline(node);

  return (
    <span className="node-dsl" aria-label={`expression at ${path}`}>
      {tokenSpans(text).map((span) => (
        <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
      ))}
    </span>
  );
}
```

- [ ] **Step 5: Show it from `RuleNodeEditor`**

In `ui/apps/demo/src/builder/RuleNodeEditor.tsx`, import it:

```tsx
import { NodeDsl } from './NodeDsl.js';
```

Add the derived flag beside `collapsed`:

```tsx
  // A leaf's tree form and its text form are the same string, so it has nothing to toggle
  // between and is always shown as DSL.
  const inDslView = !hasChildren || collapsed;
```

Replace the contents of `<span className="node-body">` with:

```tsx
        <span className="node-body">
          {inDslView ? (
            <NodeDsl path={path} node={node} modelType={modelType} catalog={catalog} />
          ) : (
            <>
              <span className={`node-badge node-badge-${summary.kind}`}>{summary.badge}</span>
              {summary.description && <span className="node-desc">{summary.description}</span>}
              {node.name && <span className="node-name">as &quot;{node.name}&quot;</span>}
            </>
          )}
        </span>
```

- [ ] **Step 6: Style the DSL row**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* A node's DSL text. `min-width: 0` is load-bearing: `.node-row` is a flex container, and a flex
   item's default `min-width: auto` floors it at its content width — without this the ellipsis
   never engages and a long expression pushes the row controls off the end. */
.node-dsl {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font: 12.5px/1.4 var(--mono);
  color: var(--dsl-fg);
}

/* Read-state token colours, sharing the custom properties the CodeMirror highlight style binds
   to, so a row reads identically before and after it is focused for editing. */
.tok-spec { color: var(--dsl-spec); }
.tok-paramRef { color: var(--dsl-param); }
.tok-keyword,
.tok-quantifier { color: var(--dsl-keyword); }
.tok-type { color: var(--dsl-type); }
.tok-operator { color: var(--dsl-operator); }
.tok-paren,
.tok-brace { color: var(--dsl-bracket); }
.tok-colon,
.tok-equals { color: var(--dsl-punctuation); }
.tok-string { color: var(--dsl-string); }
.tok-expression { color: var(--dsl-expression); }
.tok-number { color: var(--dsl-number); }
.tok-error { color: var(--danger); }
```

- [ ] **Step 7: Run the tests**

Run: `cd ui/apps/demo && pnpm vitest run && pnpm typecheck`

Expected: PASS. `NodeDsl.test.tsx` green, and the Task 3 tests still green.

- [ ] **Step 8: Commit**

```bash
git add ui/apps/demo/src/builder/NodeDsl.tsx ui/apps/demo/src/builder/dslTokens.ts ui/apps/demo/src/builder/RuleNodeEditor.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/NodeDsl.test.tsx
git commit -m "feat(demo): render collapsed subtrees and leaves as DSL text"
```

---

### Task 6: Make the DSL rows editable

**Files:**
- Modify: `ui/apps/demo/src/builder/NodeDsl.tsx`
- Modify: `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/builder/NodeDsl.test.tsx`

**Interfaces:**
- Consumes: `NodeDsl` (Task 5); `parse` from `@motiv/rules-core`; `useRuleEditorStore` from `@motiv/rules-react`; `motiv`, `motivEditorTheme`, `createMotivCompletion` from `../dsl/`; `editorView`, `replaceBuffer` from `../../test/support/codemirror.js` in tests.
- Produces: the aria-label `edit expression at {path}` on the read-state button; a commit that calls `store.replaceNode(path, parse(text).document.rule)`.

No central "which row is editing" state: the DOM already enforces that one element has focus, so a local `editing` boolean per row yields exactly one live CodeMirror for free.

- [ ] **Step 1: Write the failing tests**

Append to `ui/apps/demo/test/builder/NodeDsl.test.tsx` (add `replaceBuffer` to the imports from `../support/codemirror.js`):

```tsx
describe('DSL row editing', () => {
  const focusRow = async (path: string) => {
    fireEvent.focus(await screen.findByRole('button', { name: `edit expression at ${path}` }));
  };

  it('commits a valid edit into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-adult & is-active');
    fireEvent.keyDown(container.querySelector('.cm-content')!, { key: 'Enter' });
    expect(store.getState().document.rule).toEqual({
      and: [{ spec: 'is-adult' }, { spec: 'is-active' }],
    });
  });

  it('blocks an unparseable edit and leaves the document alone', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-active &');
    fireEvent.keyDown(container.querySelector('.cm-content')!, { key: 'Enter' });
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
    expect(screen.getByRole('alert').textContent).toMatch(/expected|unexpected/i);
  });

  it('escape reverts to the node as it stands', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { container } = renderWith(store);
    await focusRow('$.rule');
    replaceBuffer(container, 'is-adult');
    fireEvent.keyDown(container.querySelector('.cm-content')!, { key: 'Escape' });
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
    expect(screen.getByLabelText('expression at $.rule').textContent).toBe('is-active');
  });

  it('round-trips a focus-and-blur with no edit', async () => {
    const rule = { asAtLeastNSatisfied: { spec: 'is-active' }, n: '@minOrders', path: 'orders' };
    const store = new RuleEditorStore({
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule,
    });
    const { container } = renderWith(store);
    fireEvent.click(await screen.findByRole('button', { name: 'collapse $.rule' }));
    await focusRow('$.rule');
    fireEvent.blur(container.querySelector('.cm-content')!);
    expect(store.getState().document.rule).toEqual(rule);
  });
});
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/NodeDsl.test.tsx`

Expected: FAIL — no `edit expression at $.rule` button.

- [ ] **Step 3: Rewrite `NodeDsl` with an edit state**

Replace `ui/apps/demo/src/builder/NodeDsl.tsx` with:

```tsx
import { useEffect, useRef, useState } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap } from '@codemirror/view';
import { parse, printInline, type Catalog, type RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { createMotivCompletion } from '../dsl/completion.js';
import { motiv } from '../dsl/motivLanguage.js';
import { motivEditorTheme } from '../dsl/theme.js';
import { tokenSpans } from './dslTokens.js';

/** Keeps a row to one line: a pasted newline would silently grow the row out of the tree. */
const singleLine = EditorState.transactionFilter.of((tr) => (tr.newDoc.lines > 1 ? [] : tr));

/**
 * A node rendered as one line of DSL — what a leaf always shows, and what a parent shows once
 * its subtree is collapsed — and, on focus, edited as text.
 *
 * The read state is static highlighted spans, so a tree of any size costs no editors. Focus
 * swaps in a CodeMirror instance; because only one element can hold focus, exactly one is ever
 * mounted without any central bookkeeping.
 *
 * A commit parses the buffer and splices the result in through `replaceNode`. An unparseable
 * buffer is refused and the text is left as typed — the invalid state lives only in the editor,
 * never in the document, exactly as the DSL pane's uncommitted buffer does.
 */
export function NodeDsl(props: { path: string; node: RuleNode; modelType: string; catalog: Catalog }) {
  const { path, node, modelType, catalog } = props;
  const store = useRuleEditorStore();
  const text = printInline(node);

  const [editing, setEditing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const host = useRef<HTMLSpanElement | null>(null);
  /** The latest render's values, for the once-built extensions to read. */
  const live = useRef({ path, text, catalog, modelType });
  live.current = { path, text, catalog, modelType };

  const stop = (): void => {
    setEditing(false);
    setError(null);
  };

  const commit = (buffer: string): boolean => {
    const result = parse(buffer);
    if (!result.document || result.errors.length > 0) {
      setError(result.errors[0]?.message ?? 'could not parse this expression');
      return false;
    }
    store.replaceNode(live.current.path, result.document.rule);
    stop();
    return true;
  };

  useEffect(() => {
    const parent = host.current;
    if (!editing || !parent) return;

    // Completion is scoped to this row's model type, so a quantifier body offers only the
    // element's specs. The picker this replaced filtered the same way; the DSL pane does not.
    const scoped = (): Catalog => ({
      specs: live.current.catalog.specs.filter((spec) => spec.modelType === live.current.modelType),
      collections: live.current.catalog.collections,
    });

    const view = new EditorView({
      parent,
      state: EditorState.create({
        doc: live.current.text,
        extensions: [
          singleLine,
          history(),
          motiv(),
          motivEditorTheme,
          autocompletion({ override: [createMotivCompletion(scoped)] }),
          // Ahead of the default bindings, which would otherwise claim Enter for a newline.
          keymap.of([
            { key: 'Enter', run: (editor) => commit(editor.state.doc.toString()) },
            { key: 'Escape', run: () => { stop(); return true; } },
          ]),
          keymap.of([...defaultKeymap, ...historyKeymap, ...completionKeymap]),
          EditorView.domEventHandlers({
            blur: (_event, editor) => { commit(editor.state.doc.toString()); return false; },
          }),
        ],
      }),
    });
    view.focus();
    view.dispatch({ selection: { anchor: 0, head: view.state.doc.length } });

    return () => view.destroy();
  }, [editing]);

  if (editing) {
    return (
      <span className="node-dsl node-dsl-editing">
        <span ref={host} className="node-dsl-host" />
        {error && <span role="alert" className="error node-dsl-error">{error}</span>}
      </span>
    );
  }

  return (
    <button
      type="button"
      className="node-dsl"
      aria-label={`edit expression at ${path}`}
      onFocus={() => setEditing(true)}
      onClick={() => setEditing(true)}
    >
      <span aria-label={`expression at ${path}`}>
        {tokenSpans(text).map((span) => (
          <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
        ))}
      </span>
    </button>
  );
}
```

- [ ] **Step 4: Style the edit state**

Append to `ui/apps/demo/src/styles/app.css`:

```css
button.node-dsl {
  appearance: none;
  border: none;
  background: transparent;
  padding: 0;
  text-align: left;
  cursor: text;
}

.node-dsl-editing {
  display: flex;
  align-items: center;
  gap: calc(var(--space) / 2);
  overflow: visible;
  white-space: normal;
}

.node-dsl-host {
  flex: 1 1 auto;
  min-width: 0;
}

/* The editor sheds the pane chrome: no gutter, no page-height, no vertical padding, so the row
   keeps the height it had while it was static text. */
.node-dsl-host .cm-editor { height: auto; background: transparent; }
.node-dsl-host .cm-content { padding: 0; }

.node-dsl-error {
  flex: none;
  padding: 1px 6px;
  font-size: 11px;
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/NodeDsl.test.tsx`

Expected: PASS. If `editorView` cannot find a view, the host span mounted after the effect ran — check that `editing` gates the render, not the effect alone.

- [ ] **Step 6: Run the whole demo suite**

Run: `cd ui/apps/demo && pnpm vitest run && pnpm typecheck`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add ui/apps/demo/src/builder/NodeDsl.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/NodeDsl.test.tsx
git commit -m "feat(demo): edit a node's expression as DSL text in its row"
```

---

### Task 7: Retire the affordances DSL authoring replaces

**Files:**
- Modify: `ui/apps/demo/src/builder/NodeToolbar.tsx`
- Modify: `ui/apps/demo/src/builder/RuleNodeEditor.tsx`
- Test: `ui/apps/demo/test/builder/RuleNodeEditor.test.tsx`, `test/builder/ExtensionPoints.test.tsx`

**Interfaces:**
- Consumes: everything from Tasks 3-6.
- Produces: a `NodeToolbar` with no `spec at {path}` select and no `add quantifier to {path}` button. `insertQuantifier` stays exported from `mutations.ts` — only its button goes.

- [ ] **Step 1: Write the failing tests**

Append to `describe('BuilderPane accordion (boolean)', …)` in `ui/apps/demo/test/builder/RuleNodeEditor.test.tsx`:

```tsx
  it('no longer offers a spec select — the row is the way to change a spec', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await openDetail('$.rule');
    expect(screen.queryByLabelText('spec at $.rule')).toBeNull();
  });

  it('no longer offers an add-quantifier button', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    expect(screen.queryByRole('button', { name: 'add quantifier to $.rule' })).toBeNull();
  });

  it('still wraps, negates and adds operands', async () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    renderWith(store);
    await openDetail('$.rule');
    fireEvent.click(screen.getByRole('button', { name: 'add operand to $.rule' }));
    expect((store.getState().document.rule as { and: unknown[] }).and).toHaveLength(3);
  });
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `cd ui/apps/demo && pnpm vitest run test/builder/RuleNodeEditor.test.tsx`

Expected: FAIL — `spec at $.rule` and `add quantifier to $.rule` still exist.

- [ ] **Step 3: Trim the toolbar**

In `ui/apps/demo/src/builder/NodeToolbar.tsx`, delete the `isSpecNode(node) && (<label className="field">…</label>)` block (the spec select) and the `isBinaryNode(node) && (<button … aria-label={`add quantifier to ${path}`}>)` block. Drop `isSpecNode` from the import if the disabled `expression — coming` button is the only remaining user — it is, so keep it.

Update the component's doc comment:

```tsx
/**
 * The structural edit controls for a rule node: NOT, wrap, add/remove operand. They live inside
 * the node's detail panel, which is closed by default.
 *
 * There is deliberately no spec picker here. A node's expression is edited as DSL text in its
 * own row, where completion offers the same catalog specs scoped the same way — so a picker
 * would be a second, narrower way to do what the row already does.
 */
```

- [ ] **Step 4: Focus a newly added operand**

In `ui/apps/demo/src/builder/NodeToolbar.tsx`, the `+ operand` handler adds a leaf. A leaf is already in DSL view, so it only needs focusing. Change the handler to:

```tsx
          onClick={() => {
            const index = isBinaryNode(node) ? operandsOf(node).length : 0;
            store.addOperand(path, { spec: fallbackSpec });
            // The row mounts on the next render, so the focus waits for it. Selecting the seeded
            // spec means the first keystroke replaces it, which is what makes `+ operand`
            // read as "type a new expression" rather than "insert a placeholder".
            requestAnimationFrame(() => {
              const row = document.querySelector<HTMLButtonElement>(
                `[aria-label="edit expression at ${path}.${binaryOperator(node)}[${index}]"]`,
              );
              row?.focus();
            });
          }}
```

Add `binaryOperator` and `operandsOf` to the `@motiv/rules-core` import.

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `cd ui/apps/demo && pnpm vitest run && pnpm typecheck`

Expected: PASS. Fix any `ExtensionPoints.test.tsx` assertion still expecting the spec select beside the `expression — coming` button.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/src/builder ui/apps/demo/test/builder
git commit -m "feat(demo): retire the spec picker and add-quantifier button"
```

---

### Task 8: Rework the e2e suite and verify

**Files:**
- Modify: `ui/apps/demo/e2e/smoke.spec.ts`, `e2e/higher-order.spec.ts`, `e2e/dsl.spec.ts`, `e2e/live-rules.spec.ts`

**Interfaces:**
- Consumes: every aria-label produced by Tasks 3-7.
- Produces: nothing downstream.

All four specs wait on `page.getByLabel('spec at $.rule')` as their "builder is ready" signal, and that select no longer exists. The replacement readiness signal is `expression at $.rule`, which the root row always carries.

- [ ] **Step 1: Replace the readiness wait in every spec**

In each of `e2e/smoke.spec.ts:6`, `e2e/higher-order.spec.ts:6,28`, `e2e/dsl.spec.ts:5`, replace:

```ts
await expect(page.getByLabel('spec at $.rule')).toBeVisible();
```

with:

```ts
await expect(page.getByLabel('expression at $.rule')).toBeVisible();
```

- [ ] **Step 2: Replace `selectOption` with typing**

In `e2e/smoke.spec.ts` and `e2e/higher-order.spec.ts`, replace each `selectOption` on the root spec:

```ts
await page.getByLabel('spec at $.rule').selectOption('is-adult');
```

with a row edit:

```ts
await page.getByRole('button', { name: 'edit expression at $.rule' }).click();
await page.keyboard.press('ControlOrMeta+a');
await page.keyboard.type('is-adult');
await page.keyboard.press('Enter');
await expect(page.getByLabel('expression at $.rule')).toHaveText('is-adult');
```

- [ ] **Step 3: Open the detail panel before every toolbar click**

Before each `wrap $.rule in AND`, `toggle NOT at $.rule` or `add quantifier to $.rule` click in `e2e/smoke.spec.ts`, `e2e/higher-order.spec.ts` and `e2e/live-rules.spec.ts:47`, insert:

```ts
await page.getByRole('button', { name: 'details for $.rule' }).click();
```

- [ ] **Step 4: Author the quantifier as DSL in `higher-order.spec.ts`**

`add quantifier to $.rule` is gone. Replace the quantifier construction (lines 11-13 and 32-33) with a row edit on the second operand:

```ts
await page.getByRole('button', { name: 'edit expression at $.rule.and[1]' }).click();
await page.keyboard.press('ControlOrMeta+a');
await page.keyboard.type('all in orders { is-large-order }');
await page.keyboard.press('Enter');
await expect(page.getByLabel('rule document')).toContainText('asAllSatisfied');
await expect(page.getByLabel('rule document')).toContainText('"path": "orders"');
```

- [ ] **Step 5: Run the e2e suite**

Run: `cd ui/apps/demo && pnpm e2e`

Expected: PASS, all four specs.

- [ ] **Step 6: Run everything**

Run:

```bash
cd ui/packages/rules-core && pnpm vitest run && pnpm typecheck
cd ../rules-react && pnpm vitest run && pnpm typecheck
cd ../../apps/demo && pnpm vitest run && pnpm typecheck && pnpm build
```

Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add ui/apps/demo/e2e
git commit -m "test(demo): drive the builder through its DSL rows"
```

- [ ] **Step 8: Post-implementation code review (CLAUDE.md, mandatory)**

Spawn a `code-simplifier` agent over the changed files:

`ui/packages/rules-core/src/dsl/printer.ts`, `ui/apps/demo/src/builder/*.tsx`, `ui/apps/demo/src/builder/*.ts`, `ui/apps/demo/src/panes/BuilderPane.tsx`, `ui/apps/demo/src/styles/app.css`.

Ask it for: duplication between `NodeDsl`'s editor setup and `DslEditor`'s (both build a CodeMirror over the same language and completion — check whether a shared extension factory is warranted, or whether the two genuinely differ enough that extracting one would branch on flags), long methods in `RuleNodeEditor`, and any leftover indirection from the old single-`expanded` model.

Apply what it finds, re-run the affected tests, and commit.

---

## Self-Review

**Spec coverage:** two-concern split → Tasks 2, 3. Pinning and its four transitions → Tasks 2, 4. Close-all strip with reserved height → Task 4. Caret as representation toggle → Tasks 3, 5. Leaves permanently in DSL view → Task 5. `printInline` + round-trip invariant → Task 1. CSS ellipsis with `min-width: 0` → Task 5. Row anatomy → Task 3 (amended: the row body cannot be a button). Inline CodeMirror, commit/revert/error → Task 6. Completion scoping → Task 6 (the `scoped()` closure). Spec picker and `+ quantifier` removed → Task 7. `+ operand` auto-focus → Task 7. Known churn → Tasks 3, 7, 8.

**Out-of-scope items honoured:** the DSL pane's unscoped completion is untouched; no pruning of stale paths (documented in `accordion.ts`'s comment); no backend change.

**Type consistency:** `AccordionModel`/`EMPTY_ACCORDION`/`isCollapsed`/`isOpen`/`isPinned`/`toggleCollapsed`/`toggleOpen`/`togglePin`/`closeAll` are defined in Task 2 and used under those exact names in Tasks 3 and 4. `AccordionState` (the context value) is distinct from `AccordionModel` (the data) and carries `model` plus the four void-returning transitions. `printInline(node)` is defined in Task 1 and consumed in Tasks 5 and 6. `tokenSpans` returns `TokenSpan[]` with `kind: TokenKind | 'gap'`, and Task 5's CSS covers every `TokenKind` plus `.tok-gap` (uncoloured, inheriting — intentional).

**Known risk not yet resolved:** Task 6's blur-commit and Task 7's `requestAnimationFrame` focus interact — adding an operand while another row is being edited fires that row's blur commit first. Both target different paths so the writes do not race, but if Task 7's focus test proves flaky, move the focus into a `useEffect` keyed on the new child path rather than a frame callback.
