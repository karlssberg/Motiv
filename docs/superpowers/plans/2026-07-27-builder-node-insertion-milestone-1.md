# Builder Node Insertion — Milestone 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add click/tap/keyboard node insertion to the rule builder, plus a permanent one-line DSL strip above the tree that highlights the text span of the hovered or selected node.

**Architecture:** A pure planner in `@motiv/rules-core` computes candidate documents (`planInsert`, `normalizeAt`); the demo builder renders a `+` on every row that opens a phantom DSL editor, and a `RuleDslStrip` that prints the rule and marks spans via the existing path↔text span map. The planner is shared by preview and commit so the two cannot diverge. No drag in this milestone — that is Milestone 2.

**Tech Stack:** TypeScript, React 18, Vitest, `@testing-library/react`, CodeMirror 6, Playwright, pnpm workspaces.

**Design spec:** [`docs/superpowers/specs/2026-07-27-builder-node-insertion-design.md`](../specs/2026-07-27-builder-node-insertion-design.md)

## Global Constraints

- **Builder UI only.** No change to the DSL grammar (`ui/packages/rules-core/src/dsl/parser.ts`, `lexer.ts`), to `schemas/rule.v1.json`, or to anything under `src/` (C#).
- **All commands run from `ui/`** unless stated otherwise. Package manager is `pnpm@9.12.0`.
- **Demo unit tests alias workspace packages to source** (`apps/demo/vite.config.ts` `test.alias`), so a new `rules-core` export is visible to demo *tests* with no build. But `pnpm --filter @motiv/rules-demo typecheck` and `e2e` resolve `@motiv/rules-core` through `package.json` → `dist/`. **After changing `rules-core`, run `pnpm --filter @motiv/rules-core build` before typechecking or e2e-ing the demo**, or you will get phantom "export does not exist" errors.
- **No blank/placeholder rule node may ever reach the document, the store, or `RuleEditorStore` history.** `schemas/rule.v1.json` has no such node kind. Pending insertions live in React state only.
- **`xor` is never flattened or merged.** `and`/`andAlso` and `or`/`orElse` are distinct keys and never merge with each other.
- **A node carrying `name`, `whenTrue`, or `whenFalse` is never dissolved by normalization** — the payload would be destroyed.
- **New test files must use LF line endings.**
- **Demo test files mirror `src/`.** A test for `apps/demo/src/builder/X.ts` goes at `apps/demo/test/builder/X.test.ts(x)` and imports it as `../../src/builder/X.js`. The tree already has `test/builder/`, `test/dsl/`, `test/panes/`, and `test/support/`. Never put a new demo test at the flat `test/` root.
- **Drive CodeMirror through `test/support/codemirror.ts`, never `userEvent.type`.** Typing into a `contenteditable` does not work reliably in jsdom. The established pattern, from `test/builder/NodeDsl.test.tsx`:
  ```tsx
  import { render, screen, fireEvent } from '@testing-library/react';
  import { replaceBuffer } from '../support/codemirror.js';

  const content = (container: HTMLElement) => container.querySelector('.cm-content')!;
  // …
  replaceBuffer(container, 'is-adult & is-active');
  fireEvent.keyDown(content(container), { key: 'Enter' });   // or { key: 'Escape' }
  ```
  The helper also exports `editorView` and `editorText`.
- **The catalog resolves asynchronously**, so a test that renders the builder must `await screen.findBy…` for its first query rather than `getBy…`. Querying synchronously lets `useCatalog`'s promise resolve outside `act(...)`, and the resulting warning is itself a review finding. Existing builder tests mock only `getCatalog`:
  ```tsx
  const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
  const renderWith = (store: RuleEditorStore) =>
    render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);
  ```
- **Do not add dependencies.** Everything needed is already in `apps/demo/package.json`.
- **`RuleNodeEditor.tsx` is at 172 lines with five concerns.** Compose new row controls in as components; do not inline them.
- Full .NET suite (needed only at Task 13) runs as `DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH dotnet test Motiv.slnx` from the repo root. `net472` targets do not run on this machine; that is expected, not a failure.

---

## File Structure

**Created in `ui/packages/rules-core/src/`:**
| File | Responsibility |
|---|---|
| `normalize.ts` | `normalizeAt` — flatten undecorated same-operator nesting in a subtree |
| `plan.ts` | `InsertTarget`, `planInsert`, `insertTargetForRow`, `firstOperandTarget` |
| `dsl/spans.ts` | `SourceRange`, `rangeOfPath` — moved out of the demo's `lint.ts` |

**Created in `ui/apps/demo/src/builder/`:**
| File | Responsibility |
|---|---|
| `highlight.ts` | Pure hover/selection model + transitions (mirrors `accordion.ts`) |
| `RuleDslStrip.tsx` | The permanent DSL line; prints, memoises spans, renders marked segments |
| `useInlineDslEditor.ts` | The CodeMirror mount extracted from `NodeDsl`, shared with `PendingSlot` |
| `PendingSlot.tsx` | The phantom row and its editor |
| `NodeInsertButton.tsx` | The row `+` |

**Modified:**
| File | Change |
|---|---|
| `rules-core/src/editor.ts` | `applyPlan(next)` — commit a planner result to history |
| `rules-core/src/index.ts` | Export `normalize.js`, `plan.js` |
| `rules-core/src/dsl/index.ts` | Export `spans.js` |
| `demo/src/dsl/lint.ts` | Import `rangeOfPath`/`SourceRange` instead of defining them |
| `demo/src/builder/nodeSummary.ts` | `xor` parity description for >2 operands |
| `demo/src/builder/NodeDsl.tsx` | Use `useInlineDslEditor` |
| `demo/src/builder/NodeMenu.tsx` | `Insert first operand` item on operator rows |
| `demo/src/builder/RuleNodeEditor.tsx` | Host `+`, `PendingSlot`, hover/select handlers |
| `demo/src/panes/BuilderPane.tsx` | Highlight model, pending-slot state, render the strip |
| `demo/src/styles/app.css` | Strip, marks, `+`, phantom row, selected row |

---

### Task 1: `xor` parity description

`OP_DESCRIPTION.xor` currently reads `'exactly one must hold'`. That is correct for two operands and **wrong for three or more**, where the backend's left fold (`a.XOr(b).XOr(c)`) makes it parity — true when an *odd* number hold. This is a live bug, not just a labelling nicety.

**Files:**
- Modify: `ui/apps/demo/src/builder/nodeSummary.ts:29-31` (`OP_DESCRIPTION`) and `summarize`
- Test: `ui/apps/demo/test/builder/nodeSummary.test.ts` (create)

**Interfaces:**
- Consumes: nothing from earlier tasks
- Produces: nothing later tasks depend on

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import { summarize } from '../../src/builder/nodeSummary.js';

describe('summarize', () => {
  it('describes a two-operand xor as exactly one', () => {
    const node = { xor: [{ spec: 'a' }, { spec: 'b' }] };
    expect(summarize(node).description).toBe('exactly one must hold');
  });

  it('describes a three-operand xor as parity, not exactly one', () => {
    const node = { xor: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] };
    expect(summarize(node).description).toBe('an odd number must hold');
  });

  it('leaves other operators unaffected by operand count', () => {
    expect(summarize({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] }).description)
      .toBe('all must hold');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/nodeSummary.test.ts
```

Expected: the three-operand case FAILS — received `'exactly one must hold'`, expected `'an odd number must hold'`. The other two PASS.

- [ ] **Step 3: Write the minimal implementation**

In `nodeSummary.ts`, add the parity string next to `OP_DESCRIPTION` and special-case `xor` inside `summarize`'s binary branch:

```ts
/**
 * A three-or-more-operand `xor` is not "exactly one". The binders fold operands pairwise
 * (`children.Aggregate((left, right) => left.XOr(right))` in RuleBinder.cs), so an n-ary xor
 * is parity: satisfied when an odd number of operands are. Two operands are the case where
 * parity and "exactly one" coincide, which is why the shared description reads correctly there
 * and only there.
 */
const XOR_PARITY_DESCRIPTION = 'an odd number must hold';
```

Then in `summarize`, replace the binary branch:

```ts
  if (isBinaryNode(node)) {
    const op = binaryOperator(node);
    const description = op === 'xor' && operandsOf(node).length > 2
      ? XOR_PARITY_DESCRIPTION
      : OP_DESCRIPTION[op];
    return { badge: OPERATOR_LABELS[op], description, kind: 'op' };
  }
```

Add `operandsOf` to the existing `@motiv/rules-core` import list at the top of the file.

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/nodeSummary.test.ts
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/nodeSummary.ts ui/apps/demo/test/builder/nodeSummary.test.ts
git commit -m "fix: describe n-ary xor as parity rather than exactly-one"
```

---

### Task 2: `normalizeAt` in rules-core

**Files:**
- Create: `ui/packages/rules-core/src/normalize.ts`
- Modify: `ui/packages/rules-core/src/index.ts`
- Test: `ui/packages/rules-core/test/normalize.test.ts` (create)

**Interfaces:**
- Consumes: `getNode`, `setNode` from `./paths.js`; `isBinaryNode`, `binaryOperator`, `operandsOf`, `isNotNode`, `isHigherOrderNode`, `higherOrderKey`, `higherOrderBody` from `./document.js`
- Produces: `normalizeAt(document: RuleDocument, path: string): RuleDocument` — used by Task 3

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import { normalizeAt } from '../src/normalize.js';
import type { RuleDocument } from '../src/document.js';

const doc = (rule: unknown): RuleDocument => ({ rule } as RuleDocument);

describe('normalizeAt', () => {
  it('flattens a left-nested same-operator child', () => {
    const result = normalizeAt(doc({ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }), '$.rule');
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('flattens a right-nested same-operator child', () => {
    const result = normalizeAt(doc({ and: [{ spec: 'a' }, { and: [{ spec: 'b' }, { spec: 'c' }] }] }), '$.rule');
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('flattens recursively, so nesting of any depth collapses in one pass', () => {
    const result = normalizeAt(
      doc({ and: [{ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, { spec: 'd' }] }),
      '$.rule',
    );
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }, { spec: 'd' }] });
  });

  it('refuses to dissolve a child carrying a name', () => {
    const rule = { and: [{ and: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('refuses to dissolve a child carrying whenTrue', () => {
    const rule = { and: [{ and: [{ spec: 'a' }, { spec: 'b' }], whenTrue: 'both' }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('refuses to dissolve a child carrying whenFalse', () => {
    const rule = { and: [{ and: [{ spec: 'a' }, { spec: 'b' }], whenFalse: 'neither' }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('never flattens xor, because n-ary xor is parity rather than one-of', () => {
    const rule = { xor: [{ xor: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('never merges and with andAlso', () => {
    const rule = { and: [{ andAlso: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('never merges or with orElse', () => {
    const rule = { or: [{ orElse: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] };
    expect(normalizeAt(doc(rule), '$.rule').rule).toEqual(rule);
  });

  it('flattens andAlso into andAlso and orElse into orElse', () => {
    const result = normalizeAt(doc({ andAlso: [{ andAlso: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }), '$.rule');
    expect(result.rule).toEqual({ andAlso: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('descends through not and quantifier bodies', () => {
    const result = normalizeAt(doc({ not: { and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] } }), '$.rule');
    expect(result.rule).toEqual({ not: { and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } });
  });

  it('normalizes only the subtree at the given path, leaving siblings untouched', () => {
    const untouched = { and: [{ and: [{ spec: 'x' }, { spec: 'y' }] }, { spec: 'z' }] };
    const result = normalizeAt(
      doc({ or: [{ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }] }, untouched] }),
      '$.rule.or[0]',
    );
    expect(result.rule).toEqual({
      or: [{ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] }, untouched],
    });
  });

  it('preserves the parent node own decoration while flattening its children', () => {
    const result = normalizeAt(
      doc({ and: [{ and: [{ spec: 'a' }, { spec: 'b' }] }, { spec: 'c' }], name: 'outer' }),
      '$.rule',
    );
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }], name: 'outer' });
  });

  it('leaves a leaf untouched', () => {
    expect(normalizeAt(doc({ spec: 'a' }), '$.rule').rule).toEqual({ spec: 'a' });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/normalize.test.ts
```

Expected: FAIL — `Failed to resolve import "../src/normalize.js"`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/packages/rules-core/src/normalize.ts`:

```ts
import {
  binaryOperator, higherOrderBody, higherOrderKey, isBinaryNode, isHigherOrderNode, isNotNode,
  operandsOf, type BinaryOperator, type RuleNode, type RuleDocument,
} from './document.js';
import { getNode, setNode } from './paths.js';

/**
 * The operators whose nesting is safe to dissolve. `xor` is absent deliberately: the binders fold
 * operands pairwise, so an n-ary `xor` is parity ("an odd number hold") rather than one-of, and a
 * flattened `xor` would invite the wrong reading of a document that already means something else.
 *
 * `and`/`andAlso` and `or`/`orElse` are distinct keys with distinct short-circuit semantics, so a
 * run only ever merges into a parent carrying the *same* key. That falls out of the equality check
 * below rather than needing a rule of its own.
 */
const FLATTENABLE: readonly BinaryOperator[] = ['and', 'or', 'andAlso', 'orElse'];

/**
 * True when dissolving this node would destroy something. A `name` or a `whenTrue`/`whenFalse`
 * payload belongs to the node, and a node spliced away has nowhere to put it — so decoration is
 * the signal that a nesting is deliberate rather than residual.
 */
function isDecorated(node: RuleNode): boolean {
  return node.name !== undefined || node.whenTrue !== undefined || node.whenFalse !== undefined;
}

/**
 * Rebuilds a subtree with residual same-operator nesting removed. Children are rewritten before
 * the parent merges them, so nesting of any depth collapses in this single pass: by the time a
 * child is considered for splicing it is already flat.
 */
function flatten(node: RuleNode): RuleNode {
  if (isNotNode(node)) return { ...node, not: flatten(node.not) };

  if (isHigherOrderNode(node)) {
    const key = higherOrderKey(node);
    return { ...node, [key]: flatten(higherOrderBody(node)) } as unknown as RuleNode;
  }

  if (!isBinaryNode(node)) return node;

  const operator = binaryOperator(node);
  const children = operandsOf(node).map(flatten);
  const merged = FLATTENABLE.includes(operator)
    ? children.flatMap((child) => (
      isBinaryNode(child) && binaryOperator(child) === operator && !isDecorated(child)
        ? operandsOf(child)
        : [child]
    ))
    : children;

  return { ...node, [operator]: merged } as unknown as RuleNode;
}

/**
 * Returns a new document with residual same-operator nesting removed from the subtree at `path`.
 * Scoped rather than document-wide on purpose: a hand-authored document, or one round-tripped
 * through the DSL, is displayed as authored, and a mutation only ever tidies what it touched.
 */
export function normalizeAt(document: RuleDocument, path: string): RuleDocument {
  const node = getNode(document, path);
  if (!node) return document;
  return setNode(document, path, flatten(node));
}
```

Add to `ui/packages/rules-core/src/index.ts`, after the `paths.js` line:

```ts
export * from './normalize.js';
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/normalize.test.ts
pnpm --filter @motiv/rules-core typecheck
```

Expected: all tests passed; typecheck clean.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/normalize.ts ui/packages/rules-core/src/index.ts ui/packages/rules-core/test/normalize.test.ts
git commit -m "feat(rules-core): normalizeAt flattens undecorated same-operator nesting"
```

---

### Task 3: `planInsert` and its target derivation

**Files:**
- Create: `ui/packages/rules-core/src/plan.ts`
- Modify: `ui/packages/rules-core/src/index.ts`
- Test: `ui/packages/rules-core/test/plan.test.ts` (create)

**Interfaces:**
- Consumes: `normalizeAt` from Task 2; `getNode`, `setNode`, `splitLast`, `joinSteps` from `./paths.js`
- Produces, all used by Tasks 11 and 12:
  - `type InsertTarget = { kind: 'slot'; parentPath: string; index: number } | { kind: 'wrap'; path: string }`
  - `planInsert(document: RuleDocument, target: InsertTarget, node: RuleNode): RuleDocument`
  - `insertTargetForRow(path: string): InsertTarget`
  - `firstOperandTarget(operatorPath: string): InsertTarget`

**Note on the target union:** the spec sketched three kinds, including `append` onto an operator row. `append` *is* a slot — `{kind:'slot', parentPath: operatorPath, index: operands.length}` — so the planner carries two kinds and Milestone 2's drag UI maps its third target kind onto a slot. Two kinds means one fewer branch to keep correct.

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import { firstOperandTarget, insertTargetForRow, planInsert } from '../src/plan.js';
import type { RuleDocument } from '../src/document.js';

const doc = (rule: unknown): RuleDocument => ({ rule } as RuleDocument);
const NEW = { spec: 'new' };

describe('insertTargetForRow', () => {
  it('targets the slot after an operand row', () => {
    expect(insertTargetForRow('$.rule.and[1]')).toEqual({ kind: 'slot', parentPath: '$.rule', index: 2 });
  });

  it('wraps a row that is not an operand of a list', () => {
    expect(insertTargetForRow('$.rule')).toEqual({ kind: 'wrap', path: '$.rule' });
    expect(insertTargetForRow('$.rule.not')).toEqual({ kind: 'wrap', path: '$.rule.not' });
    expect(insertTargetForRow('$.rule.asAllSatisfied')).toEqual({ kind: 'wrap', path: '$.rule.asAllSatisfied' });
  });
});

describe('firstOperandTarget', () => {
  it('targets index 0 of the operator own list', () => {
    expect(firstOperandTarget('$.rule.or[2]')).toEqual({ kind: 'slot', parentPath: '$.rule.or[2]', index: 0 });
  });
});

describe('planInsert into a slot', () => {
  it('inserts at the index, shifting later operands right', () => {
    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 1 }, NEW);
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, NEW, { spec: 'b' }] });
  });

  it('appends when the index equals the operand count', () => {
    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 2 }, NEW);
    expect(result.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, NEW] });
  });

  it('inserts at index 0', () => {
    const result = planInsert(doc({ or: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'slot', parentPath: '$.rule', index: 0 }, NEW);
    expect(result.rule).toEqual({ or: [NEW, { spec: 'a' }, { spec: 'b' }] });
  });

  it('preserves the parent decoration', () => {
    const result = planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }], name: 'outer' }), { kind: 'slot', parentPath: '$.rule', index: 0 }, NEW);
    expect(result.rule).toEqual({ and: [NEW, { spec: 'a' }, { spec: 'b' }], name: 'outer' });
  });

  it('throws when the parent is not an operator node', () => {
    expect(() => planInsert(doc({ spec: 'a' }), { kind: 'slot', parentPath: '$.rule', index: 0 }, NEW))
      .toThrow(/not an operator node/);
  });
});

describe('planInsert wrapping', () => {
  it('wraps a leaf in and', () => {
    expect(planInsert(doc({ spec: 'a' }), { kind: 'wrap', path: '$.rule' }, NEW).rule)
      .toEqual({ and: [{ spec: 'a' }, NEW] });
  });

  it('flattens into an existing undecorated and, so a wrap becomes an append', () => {
    expect(planInsert(doc({ and: [{ spec: 'a' }, { spec: 'b' }] }), { kind: 'wrap', path: '$.rule' }, NEW).rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, NEW] });
  });

  it('stays a genuine wrap when the wrapped node carries a name', () => {
    const named = { and: [{ spec: 'a' }, { spec: 'b' }], name: 'pair' };
    expect(planInsert(doc(named), { kind: 'wrap', path: '$.rule' }, NEW).rule)
      .toEqual({ and: [named, NEW] });
  });

  it('wraps a quantifier body without disturbing the quantifier', () => {
    const result = planInsert(
      doc({ asAllSatisfied: { spec: 'a' }, path: '$.orders' }),
      { kind: 'wrap', path: '$.rule.asAllSatisfied' },
      NEW,
    );
    expect(result.rule).toEqual({ asAllSatisfied: { and: [{ spec: 'a' }, NEW] }, path: '$.orders' });
  });
});

```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/plan.test.ts
```

Expected: FAIL — `Failed to resolve import "../src/plan.js"`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/packages/rules-core/src/plan.ts`:

```ts
import {
  binaryOperator, isBinaryNode, operandsOf,
  type BinaryOperator, type RuleDocument, type RuleNode,
} from './document.js';
import { normalizeAt } from './normalize.js';
import { getNode, setNode, splitLast } from './paths.js';

/**
 * Where an insertion goes. Two kinds, not three: appending onto an operator row is a `slot` whose
 * index is the operand count, so it needs no case of its own.
 *
 * - `slot` — become operand `index` of the n-ary operator at `parentPath`.
 * - `wrap` — replace the node at `path` with `and: [thatNode, inserted]`. This is how a position
 *   beside a node with no operand list of its own — the root rule, a NOT's child, a quantifier's
 *   body — is expressed.
 */
export type InsertTarget =
  | { kind: 'slot'; parentPath: string; index: number }
  | { kind: 'wrap'; path: string };

/** The operator a `wrap` introduces. Never inferred: the new parent's picker sits one click away. */
const WRAP_OPERATOR: BinaryOperator = 'and';

/**
 * The target for the `+` on the row at `path`, which means the same thing on every row: *insert a
 * sibling immediately after me*.
 *
 * A row that is an operand — its path ends in `[i]` — resolves to the slot after it. Every other
 * row has no list to be a sibling within, so "after me" is expressed as a wrap.
 *
 * One button per row cannot reach every slot, and no assignment fixes that: a row participates in
 * both its parent's list and its own children's, so `and: [a, {or: [b, c]}, d]` offers seven slots
 * to six rows. The unreachable position — before an operator's first child — is served by
 * {@link firstOperandTarget} from the row's menu instead.
 */
export function insertTargetForRow(path: string): InsertTarget {
  if (!path.endsWith(']')) return { kind: 'wrap', path };
  const { parentPath, step } = splitLast(path);
  return { kind: 'slot', parentPath, index: step.index! + 1 };
}

/** The target for `Insert first operand` on the operator row at `operatorPath`. */
export function firstOperandTarget(operatorPath: string): InsertTarget {
  return { kind: 'slot', parentPath: operatorPath, index: 0 };
}

/**
 * A new document with `node` inserted at `target`, then normalized at the point of change.
 *
 * Pure: this is the same function the preview prints and the commit applies, so a preview cannot
 * describe a mutation different from the one it triggers.
 */
export function planInsert(document: RuleDocument, target: InsertTarget, node: RuleNode): RuleDocument {
  if (target.kind === 'wrap') {
    const existing = getNode(document, target.path);
    if (!existing) throw new Error(`No node at ${target.path}.`);
    const wrapped = { [WRAP_OPERATOR]: [existing, node] } as unknown as RuleNode;
    return normalizeAt(setNode(document, target.path, wrapped), target.path);
  }

  const parent = getNode(document, target.parentPath);
  if (!parent || !isBinaryNode(parent)) throw new Error(`${target.parentPath} is not an operator node.`);
  const operator = binaryOperator(parent);
  const operands = [...operandsOf(parent)];
  operands.splice(target.index, 0, node);
  const next = { ...parent, [operator]: operands } as unknown as RuleNode;
  return normalizeAt(setNode(document, target.parentPath, next), target.parentPath);
}
```

Add to `ui/packages/rules-core/src/index.ts`:

```ts
export * from './plan.js';
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/plan.test.ts
pnpm --filter @motiv/rules-core typecheck
```

Expected: all passed; typecheck clean.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/plan.ts ui/packages/rules-core/src/index.ts ui/packages/rules-core/test/plan.test.ts
git commit -m "feat(rules-core): pure planInsert with slot and wrap targets"
```

---

### Task 4: `RuleEditorStore.applyPlan`

The planner returns a whole candidate document. `loadDocument` clears history, which is wrong for an edit; `replaceNode` addresses a node, which a plan does not. One new method bridges them.

**Files:**
- Modify: `ui/packages/rules-core/src/editor.ts` (add after `replaceNode`, around line 51)
- Test: `ui/packages/rules-core/test/editor.test.ts` (add cases to the existing `describe`)

**Interfaces:**
- Consumes: nothing
- Produces: `RuleEditorStore.applyPlan(next: RuleDocument): void` — used by Tasks 11 and 12

- [ ] **Step 1: Write the failing test**

Append inside the existing `describe('RuleEditorStore edits', ...)` in `test/editor.test.ts`:

```ts
  it('applies a planned document and keeps it undoable', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const listener = vi.fn();
    store.subscribe(listener);

    store.applyPlan({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });

    expect(store.getState().document.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }] });
    expect(store.getState().canUndo).toBe(true);
    expect(listener).toHaveBeenCalledOnce();

    store.undo();
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
  });
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/editor.test.ts
```

Expected: FAIL — `store.applyPlan is not a function`.

- [ ] **Step 3: Write the minimal implementation**

In `editor.ts`, directly after `replaceNode`:

```ts
  /**
   * Commits a document produced by the planner, as one undoable edit.
   *
   * Distinct from `loadDocument`, which installs a fresh baseline and clears history: a planned
   * insertion or move is an edit like any other and must be undoable. Distinct from `replaceNode`
   * because a plan is not addressed to a node — normalization may have rewritten a parent, or
   * collapsed one, above the point of change.
   */
  applyPlan(next: RuleDocument): void {
    this.#commit(next);
  }
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/editor.test.ts
pnpm --filter @motiv/rules-core typecheck
```

Expected: all passed.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/editor.ts ui/packages/rules-core/test/editor.test.ts
git commit -m "feat(rules-core): applyPlan commits a planned document as one undoable edit"
```

---

### Task 5: move `rangeOfPath` into rules-core

The strip needs path→text-range, which already exists as a private helper in the demo's linter. Move it so both consumers share one implementation.

**Files:**
- Create: `ui/packages/rules-core/src/dsl/spans.ts`
- Modify: `ui/packages/rules-core/src/dsl/index.ts`; `ui/apps/demo/src/dsl/lint.ts:11-38` (delete `SourceRange`, `parentPath`, `rangeOfPath`; import them)
- Test: `ui/packages/rules-core/test/spans.test.ts` (create)

**Interfaces:**
- Consumes: `NodeSpan` from `./types.js`
- Produces, used by Task 7: `SourceRange { from: number; to: number }`; `rangeOfPath(path: string, spans: readonly NodeSpan[], documentLength: number): SourceRange`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import { rangeOfPath } from '../src/dsl/spans.js';
import { parse } from '../src/dsl/parser.js';
import { printInline } from '../src/dsl/printer.js';

describe('rangeOfPath', () => {
  const spansOf = (text: string) => parse(text).spans;

  it('finds the range recorded for an exact path', () => {
    const text = 'a & b';
    const range = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('b');
  });

  it('includes the parentheses of a grouped subtree', () => {
    const text = 'a & (b | c)';
    const range = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('(b | c)');
  });

  it('falls back to the nearest ancestor for a sub-field path', () => {
    const text = 'a & b';
    const exact = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(rangeOfPath('$.rule.and[1].whenTrue', spansOf(text), text.length)).toEqual(exact);
  });

  it('falls back to the whole document for an unknown path', () => {
    const text = 'a & b';
    expect(rangeOfPath('$.rule.or[9]', spansOf(text), text.length)).toEqual({ from: 0, to: text.length });
  });

  it('round-trips a printed node so a builder document can be addressed by path', () => {
    const rule = { and: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] };
    const text = printInline(rule);
    const range = rangeOfPath('$.rule.and[1]', spansOf(text), text.length);
    expect(text.slice(range.from, range.to)).toBe('(b | c)');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/spans.test.ts
```

Expected: FAIL — `Failed to resolve import "../src/dsl/spans.js"`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/packages/rules-core/src/dsl/spans.ts` by moving the three helpers from `apps/demo/src/dsl/lint.ts` verbatim, exported:

```ts
import type { NodeSpan } from './types.js';

/** A half-open source range `[from, to)`. */
export interface SourceRange {
  from: number;
  to: number;
}

/** The path one level up, or null once the root is reached. */
function parentPath(path: string): string | null {
  const index = path.lastIndexOf('.');
  return index <= 0 ? null : path.slice(0, index);
}

/**
 * The span recorded for `path`, or for its nearest ancestor that has one — so a sub-field path
 * like `$.rule.whenTrue` anchors on the node that owns it. Falls back to the whole document.
 *
 * The parser guarantees one span per path, widened to cover any parentheses and `as` clause, so a
 * grouped subtree resolves to a range including its parens rather than to the bare inner text.
 */
export function rangeOfPath(
  path: string,
  spans: readonly NodeSpan[],
  documentLength: number,
): SourceRange {
  for (let current: string | null = path; current !== null; current = parentPath(current)) {
    const span = spans.find((candidate) => candidate.path === current);
    if (span) return { from: span.from, to: span.to };
  }
  return { from: 0, to: documentLength };
}
```

Add to `ui/packages/rules-core/src/dsl/index.ts`:

```ts
export * from './spans.js';
```

In `apps/demo/src/dsl/lint.ts`: delete the local `SourceRange` interface, `parentPath`, and `rangeOfPath` (lines 11–38), and add `SourceRange` and `rangeOfPath` to the existing `@motiv/rules-core` import. `SourceRange` is a type-only import; `nonEmpty` stays local — it is a linter concern, not a span concern.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pnpm --filter @motiv/rules-core exec vitest run test/spans.test.ts
pnpm --filter @motiv/rules-core typecheck
pnpm --filter @motiv/rules-core build
pnpm --filter @motiv/rules-demo exec vitest run
pnpm --filter @motiv/rules-demo typecheck
```

Expected: all passed. The `build` before the demo typecheck is required — see Global Constraints.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/dsl/spans.ts ui/packages/rules-core/src/dsl/index.ts ui/packages/rules-core/test/spans.test.ts ui/apps/demo/src/dsl/lint.ts
git commit -m "refactor: move rangeOfPath into rules-core for the builder strip to share"
```

---

### Task 6: the pure highlight model

Hover and selection both mark the strip, and the strip auto-scrolls to whichever *changed last*. Deciding "changed last" in the component means comparing previous props during render; deciding it in the setters is explicit. So the model is a small pure module with transitions, exactly as `accordion.ts` already is for the accordion.

**Files:**
- Create: `ui/apps/demo/src/builder/highlight.ts`
- Test: `ui/apps/demo/test/builder/highlight.test.ts` (create)

**Interfaces:**
- Consumes: nothing
- Produces, used by Tasks 7 and 8:
  - `interface HighlightModel { hoveredPath: string | null; selectedPath: string | null; focus: 'hover' | 'selection' | null }`
  - `const EMPTY_HIGHLIGHT: HighlightModel`
  - `setHovered(model: HighlightModel, path: string | null): HighlightModel`
  - `setSelected(model: HighlightModel, path: string | null): HighlightModel`
  - `focusedPath(model: HighlightModel): string | null`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import {
  EMPTY_HIGHLIGHT, focusedPath, setHovered, setSelected,
} from '../../src/builder/highlight.js';

describe('highlight model', () => {
  it('starts with nothing marked', () => {
    expect(EMPTY_HIGHLIGHT).toEqual({ hoveredPath: null, selectedPath: null, focus: null });
    expect(focusedPath(EMPTY_HIGHLIGHT)).toBeNull();
  });

  it('hovering takes focus', () => {
    const model = setHovered(EMPTY_HIGHLIGHT, '$.rule.and[0]');
    expect(model.hoveredPath).toBe('$.rule.and[0]');
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });

  it('selecting takes focus and does not clear the hover', () => {
    const model = setSelected(setHovered(EMPTY_HIGHLIGHT, '$.rule.and[0]'), '$.rule.and[1]');
    expect(model.hoveredPath).toBe('$.rule.and[0]');
    expect(model.selectedPath).toBe('$.rule.and[1]');
    expect(focusedPath(model)).toBe('$.rule.and[1]');
  });

  it('hovering after selecting takes focus back, selection intact', () => {
    const model = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), '$.rule.and[0]');
    expect(model.selectedPath).toBe('$.rule.and[1]');
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });

  it('leaving the tree hands focus back to the selection', () => {
    const model = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), null);
    expect(model.hoveredPath).toBeNull();
    expect(focusedPath(model)).toBe('$.rule.and[1]');
  });

  it('leaving the tree with nothing selected leaves nothing focused', () => {
    expect(focusedPath(setHovered(setHovered(EMPTY_HIGHLIGHT, '$.rule'), null))).toBeNull();
  });

  it('deselecting hands focus to the hover when there is one', () => {
    const model = setSelected(setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule'), '$.rule.and[0]'), null);
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/highlight.test.ts
```

Expected: FAIL — cannot resolve `../src/builder/highlight.js`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/apps/demo/src/builder/highlight.ts`:

```ts
/**
 * Which node the strip marks, and which mark it scrolls to.
 *
 * Hover and selection coexist — you can select a parent and then run the pointer over its
 * children, which is the normal traffic pattern rather than an edge case. They are drawn on
 * different axes (selection underlines, hover fills) so a nested pair stays legible.
 *
 * `focus` records which of the two changed most recently, which is the scroll target. Keeping it
 * here rather than in the strip means the decision is made by the transition that knows the answer,
 * instead of reconstructed by comparing previous props during a render.
 */
export interface HighlightModel {
  hoveredPath: string | null;
  selectedPath: string | null;
  focus: 'hover' | 'selection' | null;
}

export const EMPTY_HIGHLIGHT: HighlightModel = {
  hoveredPath: null, selectedPath: null, focus: null,
};

/** Records the row under the pointer, or `null` on leaving the tree. */
export function setHovered(model: HighlightModel, path: string | null): HighlightModel {
  // Leaving hands focus back to the selection rather than leaving a dangling 'hover' focus
  // pointing at nothing.
  return { ...model, hoveredPath: path, focus: path === null ? 'selection' : 'hover' };
}

/** Records the selected row, or `null` on deselecting. */
export function setSelected(model: HighlightModel, path: string | null): HighlightModel {
  return { ...model, selectedPath: path, focus: path === null ? 'hover' : 'selection' };
}

/** The path the strip scrolls into view: the most recently changed of the two, if it is set. */
export function focusedPath(model: HighlightModel): string | null {
  return model.focus === 'hover' ? model.hoveredPath : model.selectedPath;
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/highlight.test.ts
```

Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/highlight.ts ui/apps/demo/test/builder/highlight.test.ts
git commit -m "feat(builder): pure hover/selection highlight model"
```

---

### Task 7: `RuleDslStrip`

**Files:**
- Create: `ui/apps/demo/src/builder/RuleDslStrip.tsx`
- Modify: `ui/apps/demo/src/styles/app.css` (append)
- Test: `ui/apps/demo/test/builder/RuleDslStrip.test.tsx` (create)

**Interfaces:**
- Consumes: `rangeOfPath`, `SourceRange` (Task 5); `HighlightModel`, `focusedPath` (Task 6); `parse`, `printInline` from `@motiv/rules-core`
- Produces, used by Task 8: `RuleDslStrip(props: { rule: RuleNode; highlight: HighlightModel })`

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EMPTY_HIGHLIGHT, setHovered, setSelected } from '../../src/builder/highlight.js';
import { RuleDslStrip } from '../../src/builder/RuleDslStrip.js';

const rule = { and: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] };

describe('RuleDslStrip', () => {
  it('renders the whole rule as one line of DSL', () => {
    render(<RuleDslStrip rule={rule} highlight={EMPTY_HIGHLIGHT} />);
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & (b | c)');
  });

  it('marks nothing when nothing is hovered or selected', () => {
    const { container } = render(<RuleDslStrip rule={rule} highlight={EMPTY_HIGHLIGHT} />);
    expect(container.querySelectorAll('.dsl-strip-hover, .dsl-strip-selected')).toHaveLength(0);
  });

  it('fills the hovered subtree span, parentheses included', () => {
    const { container } = render(
      <RuleDslStrip rule={rule} highlight={setHovered(EMPTY_HIGHLIGHT, '$.rule.and[1]')} />,
    );
    const marked = [...container.querySelectorAll('.dsl-strip-hover')].map((el) => el.textContent).join('');
    expect(marked).toBe('(b | c)');
  });

  it('underlines the selected subtree span', () => {
    const { container } = render(
      <RuleDslStrip rule={rule} highlight={setSelected(EMPTY_HIGHLIGHT, '$.rule.and[0]')} />,
    );
    const marked = [...container.querySelectorAll('.dsl-strip-selected')].map((el) => el.textContent).join('');
    expect(marked).toBe('a');
  });

  it('renders both marks at once, nesting the hover inside the selection', () => {
    const highlight = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), '$.rule.and[1].or[0]');
    const { container } = render(<RuleDslStrip rule={rule} highlight={highlight} />);
    expect([...container.querySelectorAll('.dsl-strip-selected')].map((el) => el.textContent).join(''))
      .toBe('(b | c)');
    expect([...container.querySelectorAll('.dsl-strip-hover')].map((el) => el.textContent).join(''))
      .toBe('b');
  });

  it('never drops or duplicates text, whatever the marks', () => {
    const highlight = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), '$.rule.and[0]');
    render(<RuleDslStrip rule={rule} highlight={highlight} />);
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & (b | c)');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/RuleDslStrip.test.tsx
```

Expected: FAIL — cannot resolve `../src/builder/RuleDslStrip.js`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/apps/demo/src/builder/RuleDslStrip.tsx`:

```tsx
import { useEffect, useMemo, useRef } from 'react';
import { parse, printInline, rangeOfPath, type RuleNode, type SourceRange } from '@motiv/rules-core';
import { focusedPath, type HighlightModel } from './highlight.js';

/** One run of text that carries the same set of marks throughout. */
interface Segment {
  key: string;
  value: string;
  hover: boolean;
  selected: boolean;
}

/**
 * Cuts `text` at every mark boundary, so each resulting run is uniformly inside or outside each
 * mark. Doing it this way — rather than nesting elements — is what lets a hover mark sit inside a
 * selection mark without either element having to contain the other.
 */
function segmentize(
  text: string, hover: SourceRange | null, selected: SourceRange | null,
): Segment[] {
  const cuts = new Set<number>([0, text.length]);
  for (const range of [hover, selected]) {
    if (!range) continue;
    // Clamp: a stale path can resolve past the end of a freshly reprinted expression.
    cuts.add(Math.max(0, Math.min(range.from, text.length)));
    cuts.add(Math.max(0, Math.min(range.to, text.length)));
  }
  const bounds = [...cuts].sort((a, b) => a - b);

  const covers = (range: SourceRange | null, from: number): boolean =>
    range !== null && from >= range.from && from < range.to;

  const segments: Segment[] = [];
  for (let i = 0; i < bounds.length - 1; i += 1) {
    const from = bounds[i]!;
    const to = bounds[i + 1]!;
    if (from === to) continue;
    segments.push({
      key: `seg-${from}`,
      value: text.slice(from, to),
      hover: covers(hover, from),
      selected: covers(selected, from),
    });
  }
  return segments;
}

/**
 * The permanent one-line DSL rendering of the whole rule, marking the span of the hovered and
 * selected nodes.
 *
 * The tree shows structure but destroys reading order and precedence; this line shows reading order
 * but hides structure. Neither alone says why a rule means what it means, so the correspondence
 * between them is the point — hovering a grouped subtree lights up its parentheses, which is
 * exactly what the indented tree cannot express.
 *
 * Spans are obtained by printing the rule and reparsing it. That is sound rather than expedient:
 * the printer guarantees `parse(printInline(node))` deep-equals `node`, so the reparse recovers the
 * same tree, and the demo's DSL pane derives its own spans the same way. Memoised on rule identity,
 * so a hover costs no work at all.
 */
export function RuleDslStrip(props: { rule: RuleNode; highlight: HighlightModel }) {
  const { rule, highlight } = props;

  const { text, spans } = useMemo(() => {
    const printed = printInline(rule);
    return { text: printed, spans: parse(printed).spans };
  }, [rule]);

  const range = (path: string | null): SourceRange | null =>
    (path === null ? null : rangeOfPath(path, spans, text.length));

  const segments = segmentize(text, range(highlight.hoveredPath), range(highlight.selectedPath));

  // Keep the mark the user just moved to in view. Scrolling to the *most recently changed* of the
  // two needs no tie-break: `focus` already records which that was.
  const scrollTarget = useRef<HTMLSpanElement | null>(null);
  const focus = focusedPath(highlight);
  useEffect(() => {
    scrollTarget.current?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }, [focus, text]);

  const focusIsHover = highlight.focus === 'hover';

  return (
    <div className="dsl-strip">
      <span className="dsl-strip-label">rule</span>
      <span className="dsl-strip-text" aria-label="rule expression">
        {segments.map((segment) => {
          const marks = [
            segment.selected ? 'dsl-strip-selected' : null,
            segment.hover ? 'dsl-strip-hover' : null,
          ].filter(Boolean).join(' ');
          const isTarget = focusIsHover ? segment.hover : segment.selected;
          return (
            <span
              key={segment.key}
              className={marks || undefined}
              ref={isTarget ? scrollTarget : undefined}
            >
              {segment.value}
            </span>
          );
        })}
      </span>
    </div>
  );
}
```

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* The permanent DSL line above the builder tree. It shares the band `.accordion-strip` already
   reserves, so adding it costs no vertical space and shifts nothing when a node is first pinned.

   Scrolls horizontally rather than wrapping: a rule of any size stays one line, and the strip
   scrolls whichever marked span the user just moved to into view. The mask fades both edges so
   off-screen content is visible as such without spending width on an ellipsis. */
.dsl-strip {
  display: flex;
  align-items: center;
  gap: calc(var(--space) - 2px);
  min-width: 0;
  overflow-x: auto;
  overflow-y: hidden;
  scrollbar-width: none;
  padding: 3px 6px;
  background: var(--sh-inset);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  mask-image: linear-gradient(to right, transparent 0, #000 10px, #000 calc(100% - 10px), transparent 100%);
}
.dsl-strip::-webkit-scrollbar { display: none; }

.dsl-strip-label {
  flex: 0 0 auto;
  font: 9px var(--sans);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--faint);
}

.dsl-strip-text {
  font: 11.5px/1.5 var(--mono);
  white-space: nowrap;
  color: var(--dsl-fg);
}

/* Two marks on two axes: a fill behind the hover, a rule beneath the selection. Same-axis marks
   would nest as a box inside a box, which at this size is two rectangles to pull apart — and
   hovering a child of the selected node is the common case, not a rare one.

   Selection is the quieter of the two on purpose. The selected *row* already carries a strong
   accent bar, so this is a confirmation; hover has no other indicator anywhere, so its fill is the
   only evidence it exists. Weight follows necessity, not deliberateness. */
.dsl-strip-hover {
  background: var(--accent-weak);
  border-radius: 2px;
}

.dsl-strip-selected {
  border-bottom: 2px solid var(--accent);
  padding-bottom: 1px;
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/RuleDslStrip.test.tsx
pnpm --filter @motiv/rules-demo typecheck
```

Expected: 6 passed; typecheck clean. If typecheck reports `rangeOfPath`/`SourceRange` missing, run `pnpm --filter @motiv/rules-core build` first — see Global Constraints.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/RuleDslStrip.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/RuleDslStrip.test.tsx
git commit -m "feat(builder): permanent DSL strip marking hovered and selected spans"
```

---

### Task 8: wire the strip, hover, and selection into the tree

**Files:**
- Modify: `ui/apps/demo/src/panes/BuilderPane.tsx` (state + render strip); `ui/apps/demo/src/builder/RuleNodeEditor.tsx` (context type, row handlers); `ui/apps/demo/src/styles/app.css` (selected row)
- Test: `ui/apps/demo/test/builder/BuilderHighlight.test.tsx` (create)

**Interfaces:**
- Consumes: `RuleDslStrip` (Task 7); `HighlightModel`, `EMPTY_HIGHLIGHT`, `setHovered`, `setSelected` (Task 6)
- Produces: `AccordionState` gains `highlight: HighlightModel`, `setHovered(path: string | null): void`, `setSelected(path: string | null): void` — used by Task 11

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = { specs: [], collections: [] };
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

const twoOperands = () => new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
/** The `.node-row` owning a given path, found via the row's own DSL button. */
const rowFor = async (path: string): Promise<HTMLElement> => {
  const dsl = await screen.findByRole('button', { name: `edit expression at ${path}` });
  return dsl.closest('.node-row') as HTMLElement;
};
const marked = (container: HTMLElement, cls: string): string =>
  [...container.querySelectorAll(cls)].map((el) => el.textContent).join('');

describe('builder highlight wiring', () => {
  it('renders the DSL strip for the whole rule', async () => {
    renderWith(twoOperands());
    await rowFor('$.rule.and[0]');
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & b');
  });

  it('marks the hovered row span in the strip', async () => {
    const { container } = renderWith(twoOperands());

    fireEvent.mouseOver(await rowFor('$.rule.and[1]'));

    expect(marked(container, '.dsl-strip-hover')).toBe('b');
  });

  it('clears the hover mark on leaving the row', async () => {
    const { container } = renderWith(twoOperands());
    const row = await rowFor('$.rule.and[1]');

    fireEvent.mouseOver(row);
    fireEvent.mouseOut(row);

    expect(container.querySelectorAll('.dsl-strip-hover')).toHaveLength(0);
  });

  it('selecting a row underlines its span and marks the row', async () => {
    const { container } = renderWith(twoOperands());

    fireEvent.click(await screen.findByRole('button', { name: 'select $.rule.and[0]' }));

    expect(marked(container, '.dsl-strip-selected')).toBe('a');
    expect(container.querySelector('.node-row.selected')).not.toBeNull();
  });

  it('keeps the selection mark while hovering a different row', async () => {
    const { container } = renderWith(twoOperands());

    fireEvent.click(await screen.findByRole('button', { name: 'select $.rule.and[0]' }));
    fireEvent.mouseOver(await rowFor('$.rule.and[1]'));

    expect(marked(container, '.dsl-strip-selected')).toBe('a');
    expect(marked(container, '.dsl-strip-hover')).toBe('b');
  });
});
```

**Hover uses mouse events, not pointer events.** React's `onPointerEnter`/`onPointerLeave` are synthesised from the native `pointerover`/`pointerout` pair, so `fireEvent.pointerEnter` — which does not bubble — never reaches the handler, and the test would pass or fail for reasons unrelated to the feature. Hover is also a mouse-only concept by nature: the design notes that touch has no hover at all. Pointer Events are reserved for Milestone 2's drag, where they are required precisely because touch *does* deliver them.

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/BuilderHighlight.test.tsx
```

Expected: FAIL — no element labelled `rule expression`.

- [ ] **Step 3: Write the minimal implementation**

**In `RuleNodeEditor.tsx`**, extend `AccordionState`:

```ts
  /** Which node the DSL strip marks, and which mark it scrolls to. */
  highlight: HighlightModel;
  setHovered: (path: string | null) => void;
  setSelected: (path: string | null) => void;
```

with `import { type HighlightModel } from './highlight.js';`.

Inside `RuleNodeEditor`, pull the three from `useAccordion()`, and give `.node-row` the pointer and selection handlers. Replace the opening `<div className="node-row">` with:

```tsx
      <div
        className={highlight.selectedPath === path ? 'node-row selected' : 'node-row'}
        onMouseEnter={() => setHovered(path)}
        onMouseLeave={() => setHovered(null)}
      >
```

Then add a select control to the row, immediately before `<NodeMenu …>`:

```tsx
        {/* Selection is its own control rather than a click on the row: the row body is a DSL
            editor that takes focus, and `.node-dsl` already claims click to start editing. A
            separate button also gives selection a tab stop and an accessible name, which is what
            the armed-move in Milestone 2 will need. */}
        <button
          type="button"
          className="node-select"
          aria-pressed={highlight.selectedPath === path}
          aria-label={`select ${path}`}
          onClick={() => setSelected(highlight.selectedPath === path ? null : path)}
        >
          ◈
        </button>
```

**In `BuilderPane.tsx`**, add the highlight state and render the strip. Add imports:

```ts
import { RuleDslStrip } from '../builder/RuleDslStrip.js';
import { EMPTY_HIGHLIGHT, setHovered, setSelected, type HighlightModel } from '../builder/highlight.js';
```

and extend the existing `@motiv/rules-react` import to `{ useCatalog, useRuleEditor, useRuleEditorStore }`.

Inside `BuilderBody`, after the `openPopover` state:

```ts
  const [highlight, setHighlight] = useState<HighlightModel>(EMPTY_HIGHLIGHT);
  // `useRuleEditor` takes the store explicitly and returns EditorState; the store itself comes from
  // the provider. There is no zero-argument state hook in @motiv/rules-react.
  const editorState = useRuleEditor(useRuleEditorStore());
```

Render the strip immediately above the existing `.accordion-strip` div:

```tsx
      <RuleDslStrip rule={editorState.document.rule} highlight={highlight} />
```

and add to the `AccordionContext.Provider` value:

```ts
          highlight,
          setHovered: (path) => setHighlight((prev) => setHovered(prev, path)),
          setSelected: (path) => setHighlight((prev) => setSelected(prev, path)),
```

Append to `app.css`:

```css
/* The selected row. A left accent bar rather than a fill: the row already carries a DSL editor
   whose own background must stay readable, and a bar reads at a glance down a column of rows. */
.node-row.selected {
  border-color: var(--accent);
  box-shadow: inset 2px 0 0 var(--accent);
}

.node-select {
  flex: 0 0 auto;
  background: none;
  border: none;
  padding: 0 2px;
  cursor: pointer;
  font-size: 10px;
  color: var(--faint);
  opacity: 0;
}
.node-row:hover .node-select,
.node-select:focus-visible,
.node-select[aria-pressed='true'] {
  opacity: 1;
}
.node-select[aria-pressed='true'] { color: var(--accent); }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pnpm --filter @motiv/rules-demo exec vitest run
pnpm --filter @motiv/rules-demo typecheck
```

Expected: the new file's 4 tests pass and **every pre-existing demo test still passes**. `App.test.tsx` and any builder test that counts row controls may need updating for the new button — update the assertions, do not weaken them.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/panes/BuilderPane.tsx ui/apps/demo/src/builder/RuleNodeEditor.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/BuilderHighlight.test.tsx
git commit -m "feat(builder): wire hover and selection to the DSL strip"
```

---

### Task 9: extract `useInlineDslEditor` from `NodeDsl`

`PendingSlot` needs the same one-line CodeMirror editor `NodeDsl` mounts — single-line filter, `motiv()` language, model-scoped completion, Enter to commit, Escape to cancel, commit-on-blur, and the `attached` guard that stops teardown's blur writing back. That is ~60 lines of mechanical wiring. `CLAUDE.md` warns against over-DRYing, and rightly, but the warning is aimed at abstractions with branching logic over nuanced builder paths. This is one editor configuration with no branches, and two copies would drift.

This task changes no behaviour. The existing `NodeDsl` tests are the regression net and must stay green untouched.

**Files:**
- Create: `ui/apps/demo/src/builder/useInlineDslEditor.ts`
- Modify: `ui/apps/demo/src/builder/NodeDsl.tsx`
- Test: existing demo suite (no new test file)

**Interfaces:**
- Consumes: nothing from earlier tasks
- Produces, used by Task 10:
  ```ts
  useInlineDslEditor(options: {
    active: boolean;
    initialText: string;
    scope: () => { catalog: Catalog; modelType: string };
    onCommit: (text: string) => boolean;
    onCancel: () => void;
  }): { host: RefObject<HTMLSpanElement | null> }
  ```
  `onCommit` returns `true` when the buffer was accepted; `false` leaves the editor open with the text as typed.

- [ ] **Step 1: Confirm the current tests pass, so a regression is attributable**

```bash
pnpm --filter @motiv/rules-demo exec vitest run
```

Expected: all pass. Note the count.

- [ ] **Step 2: Create the hook**

Create `ui/apps/demo/src/builder/useInlineDslEditor.ts` by moving the body of `NodeDsl`'s `useEffect` verbatim, parameterised by the options above. Move with it, unchanged and with their comments intact: the `singleLine` transaction filter, the `attached` ref and its rationale, the Enter binding that returns `true` even on a refused commit, the `updateListener` that clears the error on the next keystroke, and the `blur` handler.

The hook owns `host`, `attached`, and the editor lifecycle. It owns no error state — the caller renders the message, because `NodeDsl` and `PendingSlot` place it differently.

- [ ] **Step 3: Rewrite `NodeDsl` to consume the hook**

`NodeDsl` keeps `editing`, `error`, `text`, `scope`, and its own markup. Its `commit` becomes the `onCommit` callback: parse, on failure `setError(...)` and return `false`, on success `store.replaceNode(path, result.document.rule)`, `stop()`, return `true`. `onCancel` is `stop`.

- [ ] **Step 4: Run the whole demo suite to verify nothing changed**

```bash
pnpm --filter @motiv/rules-demo exec vitest run
pnpm --filter @motiv/rules-demo typecheck
```

Expected: the same count as Step 1, all passing, with no test file edited.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/useInlineDslEditor.ts ui/apps/demo/src/builder/NodeDsl.tsx
git commit -m "refactor(builder): extract useInlineDslEditor from NodeDsl"
```

---

### Task 10: `PendingSlot`

The phantom row. It must never write a blank node to the store — `schemas/rule.v1.json` has no such kind, so a placeholder would be invalid the instant the JSON pane or `/evaluate` saw it.

**Files:**
- Create: `ui/apps/demo/src/builder/PendingSlot.tsx`
- Modify: `ui/apps/demo/src/styles/app.css` (append)
- Test: `ui/apps/demo/test/builder/PendingSlot.test.tsx` (create)

**Interfaces:**
- Consumes: `useInlineDslEditor` (Task 9); `parse`, `type Catalog`, `type RuleNode` from `@motiv/rules-core`
- Produces, used by Task 11:
  ```ts
  PendingSlot(props: {
    modelType: string;
    catalog: Catalog;
    onCommit: (node: RuleNode) => void;
    onCancel: () => void;
  })
  ```

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PendingSlot } from '../../src/builder/PendingSlot.js';
import { replaceBuffer } from '../support/codemirror.js';

const catalog = { specs: [], collections: [] };
const content = (container: HTMLElement) => container.querySelector('.cm-content')!;

/** Renders the slot with spy callbacks, returning both so a test can assert on either. */
function renderSlot() {
  const onCommit = vi.fn();
  const onCancel = vi.fn();
  const view = render(
    <PendingSlot modelType="customer" catalog={catalog} onCommit={onCommit} onCancel={onCancel} />,
  );
  return { onCommit, onCancel, ...view };
}

describe('PendingSlot', () => {
  it('mounts a CodeMirror editor on the phantom row', () => {
    const { container } = renderSlot();
    expect(container.querySelector('.node-row-pending')).not.toBeNull();
    expect(content(container)).not.toBeNull();
  });

  it('commits a parsed node', () => {
    const { onCommit, container } = renderSlot();

    replaceBuffer(container, 'is-active');
    fireEvent.keyDown(content(container), { key: 'Enter' });

    expect(onCommit).toHaveBeenCalledWith({ spec: 'is-active' });
  });

  it('refuses an unparseable buffer and reports it without committing', () => {
    const { onCommit, container } = renderSlot();

    replaceBuffer(container, 'a &');
    fireEvent.keyDown(content(container), { key: 'Enter' });

    expect(onCommit).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toBeDefined();
  });

  it('cancels on Escape without committing', () => {
    const { onCommit, onCancel, container } = renderSlot();

    replaceBuffer(container, 'is-active');
    fireEvent.keyDown(content(container), { key: 'Escape' });

    expect(onCancel).toHaveBeenCalled();
    expect(onCommit).not.toHaveBeenCalled();
  });

  it('cancels rather than committing when the buffer is empty', () => {
    const { onCommit, onCancel, container } = renderSlot();

    fireEvent.keyDown(content(container), { key: 'Enter' });

    expect(onCommit).not.toHaveBeenCalled();
    expect(onCancel).toHaveBeenCalled();
  });

  it('retires the error message on the next keystroke', () => {
    const { container } = renderSlot();

    replaceBuffer(container, 'a &');
    fireEvent.keyDown(content(container), { key: 'Enter' });
    expect(screen.getByRole('alert')).toBeDefined();

    replaceBuffer(container, 'a & b');

    expect(screen.queryByRole('alert')).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/PendingSlot.test.tsx
```

Expected: FAIL — cannot resolve `../src/builder/PendingSlot.js`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/apps/demo/src/builder/PendingSlot.tsx`:

```tsx
import { useRef, useState } from 'react';
import { parse, type Catalog, type RuleNode } from '@motiv/rules-core';
import { useInlineDslEditor } from './useInlineDslEditor.js';

/**
 * A row that does not exist yet: an insertion point with a focused editor and nothing behind it.
 *
 * Deliberately not backed by a document node. `schemas/rule.v1.json` has no blank-node kind, so a
 * placeholder would be schema-invalid the moment the JSON pane rendered it or `/evaluate` received
 * it — and it would sit in undo history as a state the user can return to but not evaluate. So the
 * uncommitted node lives here, in React state, exactly as `NodeDsl` keeps an unparseable buffer out
 * of the document.
 *
 * An empty buffer cancels rather than erroring. Pressing Enter on an untouched slot, or clicking
 * away from one, means "never mind" — reporting "expected an expression" for it would be scolding
 * the user for changing their mind.
 */
export function PendingSlot(props: {
  modelType: string;
  catalog: Catalog;
  onCommit: (node: RuleNode) => void;
  onCancel: () => void;
}) {
  const { modelType, catalog, onCommit, onCancel } = props;
  const [error, setError] = useState<string | null>(null);

  const scope = useRef({ catalog, modelType });
  scope.current = { catalog, modelType };

  const { host } = useInlineDslEditor({
    active: true,
    initialText: '',
    scope: () => scope.current,
    onCommit: (buffer) => {
      if (buffer.trim() === '') {
        onCancel();
        return true;
      }
      const result = parse(buffer);
      if (!result.document || result.errors.length > 0) {
        setError(result.errors[0]?.message ?? 'could not parse this expression');
        return false;
      }
      onCommit(result.document.rule);
      return true;
    },
    onCancel,
  });

  return (
    <div className="node">
      <div className="node-row node-row-pending">
        <span className="node-chev">＋</span>
        <span className="node-dsl node-dsl-editing">
          <span ref={host} className="node-dsl-host" aria-label="new expression" />
          {error && <span role="alert" className="error node-dsl-error" title={error}>{error}</span>}
        </span>
      </div>
    </div>
  );
}
```

Append to `app.css`:

```css
/* The phantom row. Dashed, because it is a position rather than a node — it has no path, is in no
   document, and will either become a row or vanish. */
.node-row-pending {
  border-style: dashed;
  border-color: var(--accent);
}
```

**On the `aria-label`:** keep it on the host span — it is the editor's accessible name, which the slot needs since it has no row text of its own. The tests do not query by it; they address `.cm-content` and drive the view through `test/support/codemirror.ts`, the way `NodeDsl.test.tsx` already does.

- [ ] **Step 4: Run the test to verify it passes**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/PendingSlot.test.tsx
```

Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/PendingSlot.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/PendingSlot.test.tsx
git commit -m "feat(builder): phantom insertion slot that never enters the document"
```

---

### Task 11: the row `+`

**Files:**
- Create: `ui/apps/demo/src/builder/NodeInsertButton.tsx`
- Modify: `ui/apps/demo/src/builder/RuleNodeEditor.tsx`; `ui/apps/demo/src/panes/BuilderPane.tsx`; `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/builder/NodeInsert.test.tsx` (create)

**Interfaces:**
- Consumes: `insertTargetForRow`, `planInsert`, `type InsertTarget` (Task 3); `applyPlan` (Task 4); `PendingSlot` (Task 10)
- Produces, used by Task 12: `AccordionState` gains
  ```ts
  /** The open insertion slot, if any: a row path plus which of that row's two positions. */
  pending: { path: string; where: 'after' | 'first' } | null;
  setPending: (pending: { path: string; where: 'after' | 'first' } | null) => void;
  ```
  The `where` discriminant is introduced now, though this task only ever sets `'after'`. Task 12
  adds the second position, and typing it as a bare path here would mean rewriting this task's call
  sites and `BuilderPane`'s `useState` a task later for nothing.

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';
import { replaceBuffer } from '../support/codemirror.js';

const catalog = { specs: [], collections: [] };
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;

function renderBuilder(rule: unknown) {
  const store = new RuleEditorStore({ rule } as never);
  const view = render(
    <RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>,
  );
  return { store, ...view };
}

const slot = (container: HTMLElement) => container.querySelector('.node-row-pending .cm-content');
/** Opens the slot after `path` and types `text` into it, committing with Enter. */
const insertAfter = async (container: HTMLElement, path: string, text: string) => {
  fireEvent.click(await screen.findByRole('button', { name: `insert after ${path}` }));
  const pending = container.querySelector('.node-row-pending') as HTMLElement;
  replaceBuffer(pending, text);
  fireEvent.keyDown(slot(container)!, { key: 'Enter' });
};

describe('row + insertion', () => {
  it('inserts a sibling immediately after an operand row', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    await insertAfter(container, '$.rule.and[0]', 'c');

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'c' }, { spec: 'b' }] });
  });

  it('wraps a lone root spec in and', async () => {
    const { store, container } = renderBuilder({ spec: 'a' });

    await insertAfter(container, '$.rule', 'b');

    expect(store.getState().document.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }] });
  });

  it('appends to the root operator rather than nesting it, since the wrap normalizes away', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    await insertAfter(container, '$.rule', 'c');

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });
  });

  it('leaves the document untouched when the slot is cancelled', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });
    const before = store.getState().document;

    fireEvent.click(await screen.findByRole('button', { name: 'insert after $.rule.and[0]' }));
    fireEvent.keyDown(slot(container)!, { key: 'Escape' });

    expect(store.getState().document).toEqual(before);
    expect(store.getState().canUndo).toBe(false);
    expect(slot(container)).toBeNull();
  });

  it('opens at most one slot at a time', async () => {
    const { container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    fireEvent.click(await screen.findByRole('button', { name: 'insert after $.rule.and[0]' }));
    fireEvent.click(await screen.findByRole('button', { name: 'insert after $.rule.and[1]' }));

    expect(container.querySelectorAll('.node-row-pending')).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/NodeInsert.test.tsx
```

Expected: FAIL — no element labelled `insert after $.rule.and[0]`.

- [ ] **Step 3: Write the minimal implementation**

Create `ui/apps/demo/src/builder/NodeInsertButton.tsx`:

```tsx
/**
 * The `+` on a row. It means the same thing on every row: *insert a sibling immediately after me*.
 *
 * One rule, no per-kind cases — an earlier draft gave operator rows "insert at index 0" so that
 * every operand slot would be button-reachable, which does not work and cannot: a row sits in both
 * its parent's list and its own children's, so `and: [a, {or: [b, c]}, d]` has seven slots and six
 * rows. The position `+` cannot reach — before an operator's first child — is offered by the row's
 * `⋯` menu instead.
 *
 * Joins the hover-revealed cluster `⋯` and `📌` already form, inheriting their reveal and spacing.
 */
export function NodeInsertButton(props: { path: string; onOpen: () => void }) {
  return (
    <button
      type="button"
      className="node-insert"
      aria-label={`insert after ${props.path}`}
      onClick={props.onOpen}
    >
      ＋
    </button>
  );
}
```

**In `RuleNodeEditor.tsx`:** extend `AccordionState` with

```ts
  /** The open insertion slot, if any: a row path plus which of that row's two positions. */
  pending: { path: string; where: 'after' | 'first' } | null;
  setPending: (pending: { path: string; where: 'after' | 'first' } | null) => void;
```

Render the button immediately before `<NodeMenu …>`:

```tsx
        <NodeInsertButton path={path} onOpen={() => setPending({ path, where: 'after' })} />
```

And render the slot after the row, before the `errors` block. It goes *after* this node's own row and, for a parent, before its children — which is where "a sibling after me" appears for an operand, and where the wrap's second operand appears for a single-child parent:

```tsx
      {pending?.path === path && pending.where === 'after' && (
        <PendingSlot
          modelType={modelType}
          catalog={catalog}
          onCommit={(inserted) => {
            const target = insertTargetForRow(path);
            store.applyPlan(planInsert(store.getState().document, target, inserted));
            setPending(null);
          }}
          onCancel={() => setPending(null)}
        />
      )}
```

`store` comes from `useRuleEditorStore()` — add the import from `@motiv/rules-react` and the call alongside the existing hooks.

**In `BuilderPane.tsx`:** add the state and pass it through.

```ts
  const [pending, setPending] = useState<{ path: string; where: 'after' | 'first' } | null>(null);
```

```ts
          pending,
          setPending,
```

Append to `app.css`:

```css
.node-insert {
  flex: 0 0 auto;
  background: none;
  border: none;
  padding: 0 2px;
  cursor: pointer;
  font-size: 11px;
  line-height: 1;
  color: var(--faint);
  opacity: 0;
}
.node-row:hover .node-insert,
.node-insert:focus-visible { opacity: 1; }
.node-insert:hover { color: var(--accent); }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pnpm --filter @motiv/rules-demo exec vitest run
pnpm --filter @motiv/rules-demo typecheck
```

Expected: the new file's 5 tests pass and the whole demo suite is green.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/NodeInsertButton.tsx ui/apps/demo/src/builder/RuleNodeEditor.tsx ui/apps/demo/src/panes/BuilderPane.tsx ui/apps/demo/src/styles/app.css ui/apps/demo/test/builder/NodeInsert.test.tsx
git commit -m "feat(builder): row + inserts a sibling after any row"
```

---

### Task 12: `⋯ → Insert first operand`

The one slot the uniform `+` cannot reach.

**Files:**
- Modify: `ui/apps/demo/src/builder/NodeMenu.tsx`; `ui/apps/demo/src/builder/RuleNodeEditor.tsx`
- Test: `ui/apps/demo/test/builder/NodeInsert.test.tsx` (add a `describe`)

**Interfaces:**
- Consumes: `firstOperandTarget`, `planInsert` (Task 3); `pending`/`setPending` (Task 11)
- Produces: `NodeMenu` gains `onInsertFirst?: () => void` — present only on operator rows

- [ ] **Step 1: Write the failing test**

Append to `test/NodeInsert.test.tsx`:

```tsx
/** Opens the first-operand slot on `path` via its menu and commits `text`. */
const insertFirst = async (container: HTMLElement, path: string, text: string) => {
  fireEvent.click(await screen.findByRole('button', { name: `actions for ${path}` }));
  fireEvent.click(screen.getByRole('menuitem', { name: 'Insert first operand' }));
  const pending = container.querySelector('.node-row-pending') as HTMLElement;
  replaceBuffer(pending, text);
  fireEvent.keyDown(pending.querySelector('.cm-content')!, { key: 'Enter' });
};

describe('insert first operand', () => {
  it('inserts before the first child of an operator row', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    await insertFirst(container, '$.rule', 'z');

    expect(store.getState().document.rule)
      .toEqual({ and: [{ spec: 'z' }, { spec: 'a' }, { spec: 'b' }] });
  });

  it('reaches the slot before a nested group first child', async () => {
    const { store, container } = renderBuilder({ and: [{ spec: 'a' }, { or: [{ spec: 'b' }, { spec: 'c' }] }] });

    await insertFirst(container, '$.rule.and[1]', 'z');

    expect(store.getState().document.rule).toEqual({
      and: [{ spec: 'a' }, { or: [{ spec: 'z' }, { spec: 'b' }, { spec: 'c' }] }],
    });
  });

  it('is not offered on a leaf row, which has no operand list', async () => {
    renderBuilder({ and: [{ spec: 'a' }, { spec: 'b' }] });

    fireEvent.click(await screen.findByRole('button', { name: 'actions for $.rule.and[0]' }));

    expect(screen.queryByRole('menuitem', { name: 'Insert first operand' })).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
pnpm --filter @motiv/rules-demo exec vitest run test/builder/NodeInsert.test.tsx
```

Expected: FAIL — no menuitem named `Insert first operand`.

- [ ] **Step 3: Write the minimal implementation**

**In `NodeMenu.tsx`**, add to the props:

```ts
  /**
   * Opens an insertion slot at index 0 of this node's operand list. Absent on rows with no list.
   *
   * Here rather than beside the row's `+` because a single button per row cannot address both the
   * list a row belongs to and the list it owns — and because the menu is already where this builder
   * puts structural actions, so the item is self-labelling where a second glyph would not be.
   */
  onInsertFirst?: () => void;
```

and extend `actions`:

```ts
  const actions: MenuAction[] = [
    { label: 'Details', run: onDetails },
    ...(onInsertFirst ? [{ label: 'Insert first operand', run: onInsertFirst }] : []),
    ...(canRemove ? [{ label: 'Remove', run: () => store.removeOperand(path) }] : []),
  ];
```

**In `RuleNodeEditor.tsx`**, pass it only for binary rows, and distinguish which slot is open. Two slots can now be requested for the same row — "after me" and "first operand" — so `pending` becomes a pair:

```ts
  /** The open insertion slot, if any: a row path plus which of its two positions. */
  pending: { path: string; where: 'after' | 'first' } | null;
  setPending: (pending: { path: string; where: 'after' | 'first' } | null) => void;
```

Update `NodeInsertButton`'s handler to `setPending({ path, where: 'after' })`, and pass to `NodeMenu`:

```tsx
          onInsertFirst={isBinaryNode(node) ? () => setPending({ path, where: 'first' }) : undefined}
```

The slot's commit picks its target from `where`, and a `first` slot renders inside `.node-kids` above the children rather than after the row:

```tsx
  const slotFor = (where: 'after' | 'first') => (
    <PendingSlot
      modelType={where === 'first' ? childModelType : modelType}
      catalog={catalog}
      onCommit={(inserted) => {
        const target = where === 'first' ? firstOperandTarget(path) : insertTargetForRow(path);
        store.applyPlan(planInsert(store.getState().document, target, inserted));
        setPending(null);
      }}
      onCancel={() => setPending(null)}
    />
  );
```

Render `pending?.path === path && pending.where === 'after' && slotFor('after')` after the row, and `pending?.path === path && pending.where === 'first' && slotFor('first')` as the first child inside `.node-kids`. A `first` slot on a *collapsed* parent still needs somewhere to render — show it after the row in that case, since `.node-kids` is not mounted.

Update `BuilderPane.tsx`'s `useState` to the pair type.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
pnpm --filter @motiv/rules-demo exec vitest run
pnpm --filter @motiv/rules-demo typecheck
```

Expected: whole demo suite green, including Task 11's five tests, which the `pending` type change touches.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/NodeMenu.tsx ui/apps/demo/src/builder/RuleNodeEditor.tsx ui/apps/demo/src/panes/BuilderPane.tsx ui/apps/demo/test/builder/NodeInsert.test.tsx
git commit -m "feat(builder): Insert first operand reaches the slot + cannot"
```

---

### Task 13: E2E, full suite, and the mandatory simplifier review

Three things jsdom cannot see: whether the strip actually scrolls, whether the phantom editor really takes focus, and whether the strip's mask/overflow renders without clipping the marks. `e2e/operator.spec.ts` exists for exactly this class of problem — a `visibility: hidden` element refuses focus in a browser but not in jsdom.

**Files:**
- Create: `ui/apps/demo/e2e/insertion.spec.ts`
- Test: this task's deliverable is the passing suites

**Interfaces:**
- Consumes: everything above
- Produces: nothing

- [ ] **Step 1: Read an existing e2e spec for the harness conventions**

```bash
cat ui/apps/demo/e2e/operator.spec.ts
```

Note how it launches, how it waits for the catalog, and what selectors it uses. Follow it — do not invent a different setup.

- [ ] **Step 2: Write the e2e spec**

Create `ui/apps/demo/e2e/insertion.spec.ts` with three tests, using the launch/wait pattern from Step 1:

1. **The phantom editor receives focus.** Click the `+` on the root row, then assert `page.locator('.node-row-pending .cm-content')` is focused — `expect(...).toBeFocused()`. jsdom focuses hidden elements regardless, so only a browser can prove this.
2. **The strip scrolls a marked span into view.** Load a rule long enough to overflow the strip (build it by typing a long expression into the DSL row, or seed via the DSL pane), hover a row whose span is off-screen, and assert the strip's `scrollLeft` changed from `0`.
3. **Insertion round-trips into the DSL pane.** Insert `c` after the first operand, switch to the DSL pane, and assert its text reads the flattened `a & c & b` — proving the planner's output is expressible DSL and that normalization reached the JSON the pane renders.

- [ ] **Step 3: Run the e2e suite**

```bash
pnpm --filter @motiv/rules-core build
pnpm --filter @motiv/rules-demo e2e
```

Expected: all pass, including the pre-existing specs. The `build` is required — `e2e` runs `vite build`, which resolves `@motiv/rules-core` through `dist/`.

- [ ] **Step 4: Run every suite in the repository**

```bash
pnpm --filter @motiv/rules-core test
pnpm --filter @motiv/rules-react test
pnpm --filter @motiv/rules-demo test
pnpm --filter @motiv/rules-core typecheck
pnpm --filter @motiv/rules-react typecheck
pnpm --filter @motiv/rules-demo typecheck
```

Then, from the repository root, the .NET suite — `CLAUDE.md` requires it whenever behaviour touching justification output changes, and the example projects assert on justification strings:

```bash
DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH dotnet test Motiv.slnx
```

Expected: all green. `net472` targets not running on this machine is expected. **If anything fails, fix it — do not proceed.**

- [ ] **Step 5: Spawn the mandatory `code-simplifier` review**

`CLAUDE.md` requires this and states it is not skippable. Dispatch a `code-simplifier` agent over the changed files, asking it specifically about: duplication between `NodeDsl` and `PendingSlot` after the Task 9 extraction; whether `RuleNodeEditor` has outgrown one file now that it hosts the `+`, selection, and two slot positions; and whether `segmentize` in `RuleDslStrip` is the clearest expression of the marking logic.

Apply what it finds, then re-run the affected suites.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/e2e/insertion.spec.ts
git commit -m "test(builder): e2e cover focus, strip scrolling, and DSL round-trip"
```

---

## Milestone 2 (separate plan, after this one lands)

Not planned here, because Milestone 1 is independently shippable and M2's task boundaries depend on how M1's components actually settle. It will cover: `dropTargetsFor`, `planMove`, `isLegalTarget` (rejecting self-descendant drops), `DropStrip`, `NodeGrip`, `useDragMove` (Pointer Events, 5px threshold, `setPointerCapture`, `touch-action: none`), `⋯ → Move` armed-move, and switching the strip to prospective content during a drag.

M2 reuses `RuleDslStrip` unchanged as its preview surface. That is why the strip is built here and not there.
