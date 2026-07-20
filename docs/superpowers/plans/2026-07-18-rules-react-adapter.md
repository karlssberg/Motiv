# @motiv/rules-react (Headless React Adapter) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `@motiv/rules-react` — a thin, headless (unstyled, ARIA-correct) React adapter over `@motiv/rules-core`: hooks that bind the editor store, catalog, and evaluation to React, plus render-prop components for the rule tree and the justification/explanation tree. All state and logic already live in the core; this package only bridges it to React idiomatically.

**Architecture:** A new `ui/packages/rules-react` package that depends on `@motiv/rules-core` (workspace) and takes `react` as a peer dependency. A React context carries the `RuleEditorStore`; `useSyncExternalStore` bridges the store's `subscribe`/`getState` to React with a cached snapshot (the store returns a fresh state object per call, so the hook memoizes by comparing the immutable fields). Async hooks wrap `RulesApiClient.getCatalog`/`evaluate`. Two headless components (`RuleTree`, `JustificationTree`) own structure + ARIA and delegate all markup to render props.

**Tech Stack:** React 18, TypeScript 5, Vitest + Testing Library (jsdom), tsup. Node ≥20 + pnpm ≥9.

---

## Context for the implementer (read first)

This is **Plan 4** of the rules-engine-frontend initiative. Plan 3 (merged on this branch) built `@motiv/rules-core`, the framework-free core. This package is its React binding. The demo app, sample ASP.NET host, and Playwright E2E are a **separate Plan 5** — do not build them here.

Read the frontend design spec `docs/superpowers/specs/2026-07-16-rules-engine-frontend-ui-design.md` (§Frontend → `@motiv/rules-react`) and the Plan 3 plan `docs/superpowers/plans/2026-07-18-rules-core-typescript-package.md` for the core's public API.

### ⚠️ Environment prerequisite
Needs **Node.js ≥20 and pnpm ≥9** on PATH (already provisioned in this environment: Node 26, pnpm 9.15 — confirm with `node --version && pnpm --version`). All `pnpm` commands run **from the `ui/` directory**.

### The `@motiv/rules-core` public API you build on (all exported from its barrel)
- **Types:** `RuleDocument`, `RuleNode`, `RuleError`, `CatalogEntry`, `EvaluationResult`, `ExplanationNode`, `ExplanationRow`, `EvaluateRequest`.
- **Store:** `RuleEditorStore` (methods `getState(): EditorState`, `subscribe(listener): () => void`, plus edit verbs); `EditorState` = `{ document: RuleDocument; errors: RuleError[]; canUndo: boolean; canRedo: boolean }`. **`getState()` returns a fresh object each call**, but its fields (`document`, `errors`) are immutable references that change only on a real edit — this matters for the snapshot cache below.
- **Pure helpers:** `getNode(document, path)`, `listPaths(document): Array<{ path: string; node: RuleNode }>`, `errorsForNode(errors, path): RuleError[]`, `toExplanationView(node)`, `flattenExplanation(view, collapsedIds): ExplanationRow[]`. `ExplanationRow` = `{ id: string; depth: number; assertions: string[]; hasChildren: boolean; collapsed: boolean }`.
- **Client:** `RulesApiClient` with `getCatalog(): Promise<CatalogEntry[]>` and `evaluate(req): Promise<EvaluationResult>` (throws `RulesApiError` on non-2xx).

### Critical design decisions (do not deviate without escalating)
- **Headless only.** No CSS, no styling, no color. Components own DOM structure + ARIA roles and delegate every piece of visible markup to a render-prop (`children` as a function). The demo app (Plan 5) supplies styling.
- **`react` is a peer dependency**, not bundled. tsup externalizes it.
- **The snapshot cache is mandatory.** `useSyncExternalStore` requires `getSnapshot` to return a referentially-stable value when nothing changed. Because `store.getState()` allocates a new object each call, `useRuleEditor` caches the last snapshot and returns it unless one of the four fields changed. Skipping this causes an infinite render loop.
- **Node paths are the core's `$.rule…` strings.** Components address nodes by those paths; tree depth is derived from the path.

### File structure (all under `ui/packages/rules-react/`)
```
package.json                 (@motiv/rules-react; peer react; dep @motiv/rules-core)
tsconfig.json
tsup.config.ts
vitest.config.ts
test/setup.ts                (Testing Library cleanup)
src/
├── index.ts                 (barrel)
├── context.ts               (RuleEditorContext + provider + useRuleEditorStore)
├── useRuleEditor.ts         (store subscription with cached snapshot)
├── useRuleNode.ts           (per-path node + errors, context-based)
├── useCatalog.ts            (async catalog fetch)
├── useEvaluation.ts         (evaluate trigger + async result)
├── RuleTree.tsx             (headless flattened tree, ARIA)
└── JustificationTree.tsx    (headless explanation tree with collapse)
test/
├── useRuleEditor.test.tsx
├── useRuleNode.test.tsx
├── useCatalog.test.tsx
├── useEvaluation.test.tsx
├── RuleTree.test.tsx
└── JustificationTree.test.tsx
```

### Test commands (from `ui/`)
Per-file: `pnpm -C packages/rules-react test test/<name>.test.tsx`
Whole package: `pnpm -C packages/rules-react test`
Typecheck + build: `pnpm -C packages/rules-react typecheck && pnpm -C packages/rules-react build`

---

## Task 1: Scaffold the `@motiv/rules-react` package

**Files:**
- Create: `ui/packages/rules-react/package.json`, `tsconfig.json`, `tsup.config.ts`, `vitest.config.ts`, `test/setup.ts`, `src/index.ts`, `test/smoke.test.tsx`

- [ ] **Step 1: Create the package files**

`ui/packages/rules-react/package.json`:
```json
{
  "name": "@motiv/rules-react",
  "version": "0.1.0",
  "description": "Headless React adapter for @motiv/rules-core.",
  "license": "MIT",
  "type": "module",
  "main": "./dist/index.cjs",
  "module": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": {
      "types": "./dist/index.d.ts",
      "import": "./dist/index.js",
      "require": "./dist/index.cjs"
    }
  },
  "files": ["dist"],
  "sideEffects": false,
  "scripts": {
    "build": "tsup",
    "test": "vitest run",
    "typecheck": "tsc --noEmit"
  },
  "peerDependencies": {
    "react": ">=18"
  },
  "dependencies": {
    "@motiv/rules-core": "workspace:*"
  },
  "devDependencies": {
    "@testing-library/dom": "^10.4.0",
    "@testing-library/react": "^16.1.0",
    "@types/react": "^18.3.12",
    "@types/react-dom": "^18.3.1",
    "jsdom": "^25.0.1",
    "react": "^18.3.1",
    "react-dom": "^18.3.1",
    "tsup": "^8.3.5",
    "typescript": "^5.7.2",
    "vitest": "^2.1.8"
  }
}
```

`ui/packages/rules-react/tsconfig.json`:
```json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "outDir": "dist",
    "rootDir": ".",
    "jsx": "react-jsx",
    "types": ["node"],
    "lib": ["ES2022", "DOM", "DOM.Iterable"]
  },
  "include": ["src", "test", "tsup.config.ts", "vitest.config.ts"]
}
```

`ui/packages/rules-react/tsup.config.ts`:
```ts
import { defineConfig } from 'tsup';

export default defineConfig({
  entry: ['src/index.ts'],
  format: ['esm', 'cjs'],
  dts: true,
  clean: true,
  sourcemap: true,
  external: ['react', '@motiv/rules-core'],
});
```

`ui/packages/rules-react/vitest.config.ts`:
```ts
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    setupFiles: ['./test/setup.ts'],
  },
});
```

`ui/packages/rules-react/test/setup.ts`:
```ts
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

afterEach(() => cleanup());
```

`ui/packages/rules-react/src/index.ts`:
```ts
export {};
```

`ui/packages/rules-react/test/smoke.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

describe('react test harness', () => {
  it('renders into jsdom', () => {
    render(<div>hello</div>);
    expect(screen.getByText('hello')).toBeDefined();
  });
});
```

- [ ] **Step 2: Install and verify the harness**

Run (from `ui/`):
```bash
pnpm install
pnpm -C packages/rules-react test
```
Expected: `pnpm install` links `@motiv/rules-core` via the workspace and updates `ui/pnpm-lock.yaml`; the smoke test passes (`1 passed`).

- [ ] **Step 3: Verify build + typecheck**

Run:
```bash
pnpm -C packages/rules-react typecheck
pnpm -C packages/rules-react build
```
Expected: typecheck clean; `dist/index.js`, `dist/index.cjs`, `dist/index.d.ts` emitted.

- [ ] **Step 4: Commit**

```bash
git add ui/packages/rules-react ui/pnpm-lock.yaml
git commit -m "feat(rules-react): scaffold headless React adapter package"
```

---

## Task 2: Editor context + `useRuleEditor`

**Files:**
- Create: `ui/packages/rules-react/src/context.ts`, `ui/packages/rules-react/src/useRuleEditor.ts`
- Test: `ui/packages/rules-react/test/useRuleEditor.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-react/test/useRuleEditor.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { RuleEditorStore } from '@motiv/rules-core';
import { useRuleEditor } from '../src/useRuleEditor.js';

describe('useRuleEditor', () => {
  it('returns the current state and re-renders on edits', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result } = renderHook(() => useRuleEditor(store));

    expect(result.current.document.rule).toEqual({ spec: 'a' });
    expect(result.current.canUndo).toBe(false);

    act(() => store.replaceNode('$.rule', { spec: 'b' }));

    expect(result.current.document.rule).toEqual({ spec: 'b' });
    expect(result.current.canUndo).toBe(true);
  });

  it('returns a stable snapshot reference when nothing changed', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result, rerender } = renderHook(() => useRuleEditor(store));
    const first = result.current;
    rerender();
    expect(result.current).toBe(first);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-react test test/useRuleEditor.test.tsx
```
Expected: FAILS — `../src/useRuleEditor.js` does not exist.

- [ ] **Step 3: Write the context**

`ui/packages/rules-react/src/context.ts`:
```ts
import { createContext, createElement, useContext, type ReactNode } from 'react';
import type { RuleEditorStore } from '@motiv/rules-core';

const RuleEditorContext = createContext<RuleEditorStore | null>(null);

/** Provides a {@link RuleEditorStore} to descendant hooks and components. */
export function RuleEditorProvider(props: { store: RuleEditorStore; children: ReactNode }): ReactNode {
  return createElement(RuleEditorContext.Provider, { value: props.store }, props.children);
}

/** Returns the store from the nearest provider; throws when used outside one. */
export function useRuleEditorStore(): RuleEditorStore {
  const store = useContext(RuleEditorContext);
  if (!store) throw new Error('useRuleEditorStore must be used within a <RuleEditorProvider>.');
  return store;
}
```

- [ ] **Step 4: Write the hook**

`ui/packages/rules-react/src/useRuleEditor.ts`:
```ts
import { useCallback, useRef, useSyncExternalStore } from 'react';
import type { EditorState, RuleEditorStore } from '@motiv/rules-core';

/**
 * Subscribes a component to a {@link RuleEditorStore} and returns its current state.
 * The store allocates a fresh state object per call, so the snapshot is cached and only
 * replaced when one of its immutable fields changes — required by useSyncExternalStore.
 */
export function useRuleEditor(store: RuleEditorStore): EditorState {
  const cache = useRef<EditorState | null>(null);

  const subscribe = useCallback((onChange: () => void) => store.subscribe(onChange), [store]);

  const getSnapshot = useCallback((): EditorState => {
    const next = store.getState();
    const prev = cache.current;
    if (
      prev &&
      prev.document === next.document &&
      prev.errors === next.errors &&
      prev.canUndo === next.canUndo &&
      prev.canRedo === next.canRedo
    ) {
      return prev;
    }
    cache.current = next;
    return next;
  }, [store]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-react test test/useRuleEditor.test.tsx
```
Expected: PASS (both cases, including the stable-reference check).

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-react/src/context.ts ui/packages/rules-react/src/useRuleEditor.ts ui/packages/rules-react/test/useRuleEditor.test.tsx
git commit -m "feat(rules-react): editor context and useRuleEditor hook"
```

---

## Task 3: `useRuleNode`

**Files:**
- Create: `ui/packages/rules-react/src/useRuleNode.ts`
- Test: `ui/packages/rules-react/test/useRuleNode.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-react/test/useRuleNode.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '../src/context.js';
import { useRuleNode } from '../src/useRuleNode.js';

function wrapper(store: RuleEditorStore) {
  return ({ children }: { children: ReactNode }) =>
    createElement(RuleEditorProvider, { store, children });
}

describe('useRuleNode', () => {
  it('returns the node at a path and its errors, reacting to edits', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.setErrors([{ path: '$.rule.and[0]', code: 'UnknownSpec', message: 'nope' }]);

    const { result } = renderHook(() => useRuleNode('$.rule.and[0]'), { wrapper: wrapper(store) });

    expect(result.current.node).toEqual({ spec: 'a' });
    expect(result.current.errors).toHaveLength(1);
    expect(result.current.errors[0]!.code).toBe('UnknownSpec');

    act(() => store.replaceNode('$.rule.and[0]', { spec: 'z' }));
    expect(result.current.node).toEqual({ spec: 'z' });
  });

  it('returns undefined for a missing path', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const { result } = renderHook(() => useRuleNode('$.rule.and[3]'), { wrapper: wrapper(store) });
    expect(result.current.node).toBeUndefined();
    expect(result.current.errors).toEqual([]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-react test test/useRuleNode.test.tsx
```
Expected: FAILS — `../src/useRuleNode.js` does not exist.

- [ ] **Step 3: Write the hook**

`ui/packages/rules-react/src/useRuleNode.ts`:
```ts
import { errorsForNode, getNode, type RuleError, type RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from './context.js';
import { useRuleEditor } from './useRuleEditor.js';

/** The node at a path plus the errors anchored on it. */
export interface RuleNodeView {
  node: RuleNode | undefined;
  errors: RuleError[];
}

/** Returns the node at a path (from the nearest provider's store) and its errors, reactively. */
export function useRuleNode(path: string): RuleNodeView {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  return {
    node: getNode(state.document, path),
    errors: errorsForNode(state.errors, path),
  };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-react test test/useRuleNode.test.tsx
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-react/src/useRuleNode.ts ui/packages/rules-react/test/useRuleNode.test.tsx
git commit -m "feat(rules-react): useRuleNode hook"
```

---

## Task 4: `useCatalog` + `useEvaluation`

**Files:**
- Create: `ui/packages/rules-react/src/useCatalog.ts`, `ui/packages/rules-react/src/useEvaluation.ts`
- Test: `ui/packages/rules-react/test/useCatalog.test.tsx`, `ui/packages/rules-react/test/useEvaluation.test.tsx`

- [ ] **Step 1: Write the failing tests**

`ui/packages/rules-react/test/useCatalog.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { CatalogEntry, RulesApiClient } from '@motiv/rules-core';
import { useCatalog } from '../src/useCatalog.js';

describe('useCatalog', () => {
  it('loads the catalog and reports ready', async () => {
    const entries: CatalogEntry[] = [
      { name: 'is-positive', modelType: 'number', metadataType: 'String', isAsync: false, description: null },
    ];
    const client = { getCatalog: vi.fn().mockResolvedValue(entries) } as unknown as RulesApiClient;
    const { result } = renderHook(() => useCatalog(client));

    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.status === 'ready' && result.current.data).toEqual(entries);
  });

  it('reports error when the fetch fails', async () => {
    const client = { getCatalog: vi.fn().mockRejectedValue(new Error('boom')) } as unknown as RulesApiClient;
    const { result } = renderHook(() => useCatalog(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
  });
});
```

`ui/packages/rules-react/test/useEvaluation.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { EvaluationResult, RulesApiClient } from '@motiv/rules-core';
import { useEvaluation } from '../src/useEvaluation.js';

const result: EvaluationResult = {
  satisfied: true, reason: 'is positive', assertions: ['is positive'],
  values: ['is positive'], justification: 'is positive',
  explanation: { assertions: ['is positive'], underlying: [] },
};

describe('useEvaluation', () => {
  it('starts idle, then reports the result after evaluate()', async () => {
    const client = { evaluate: vi.fn().mockResolvedValue(result) } as unknown as RulesApiClient;
    const { result: hook } = renderHook(() => useEvaluation(client));

    expect(hook.current.status).toBe('idle');

    await act(async () => {
      await hook.current.evaluate({ modelType: 'number', document: { rule: { spec: 'is-positive' } }, model: 5 });
    });

    await waitFor(() => expect(hook.current.status).toBe('ready'));
    expect(hook.current.status === 'ready' && hook.current.result.satisfied).toBe(true);
  });

  it('reports error when evaluate rejects', async () => {
    const client = { evaluate: vi.fn().mockRejectedValue(new Error('bad')) } as unknown as RulesApiClient;
    const { result: hook } = renderHook(() => useEvaluation(client));
    await act(async () => {
      await hook.current.evaluate({ modelType: 'number', document: { rule: { spec: 'x' } }, model: 5 });
    });
    await waitFor(() => expect(hook.current.status).toBe('error'));
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
pnpm -C packages/rules-react test test/useCatalog.test.tsx test/useEvaluation.test.tsx
```
Expected: FAILS — the hook modules do not exist.

- [ ] **Step 3: Write `useCatalog`**

`ui/packages/rules-react/src/useCatalog.ts`:
```ts
import { useEffect, useState } from 'react';
import type { CatalogEntry, RulesApiClient } from '@motiv/rules-core';

/** The state of an async catalog load. */
export type CatalogState =
  | { status: 'loading' }
  | { status: 'ready'; data: CatalogEntry[] }
  | { status: 'error'; error: unknown };

/** Loads the spec catalog once per client and tracks its async state. */
export function useCatalog(client: RulesApiClient): CatalogState {
  const [state, setState] = useState<CatalogState>({ status: 'loading' });

  useEffect(() => {
    let active = true;
    setState({ status: 'loading' });
    client.getCatalog()
      .then((data) => { if (active) setState({ status: 'ready', data }); })
      .catch((error: unknown) => { if (active) setState({ status: 'error', error }); });
    return () => { active = false; };
  }, [client]);

  return state;
}
```

- [ ] **Step 4: Write `useEvaluation`**

`ui/packages/rules-react/src/useEvaluation.ts`:
```ts
import { useCallback, useState } from 'react';
import type { EvaluateRequest, EvaluationResult, RulesApiClient } from '@motiv/rules-core';

type EvaluationStatus =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; result: EvaluationResult }
  | { status: 'error'; error: unknown };

/** The evaluation state plus a trigger to run an evaluation. */
export type EvaluationState = EvaluationStatus & {
  evaluate: (request: EvaluateRequest) => Promise<void>;
};

/** Exposes an evaluate() trigger and tracks the async result. */
export function useEvaluation(client: RulesApiClient): EvaluationState {
  const [state, setState] = useState<EvaluationStatus>({ status: 'idle' });

  const evaluate = useCallback(async (request: EvaluateRequest): Promise<void> => {
    setState({ status: 'loading' });
    try {
      const result = await client.evaluate(request);
      setState({ status: 'ready', result });
    } catch (error) {
      setState({ status: 'error', error });
    }
  }, [client]);

  return { ...state, evaluate };
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:
```bash
pnpm -C packages/rules-react test test/useCatalog.test.tsx test/useEvaluation.test.tsx
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-react/src/useCatalog.ts ui/packages/rules-react/src/useEvaluation.ts ui/packages/rules-react/test/useCatalog.test.tsx ui/packages/rules-react/test/useEvaluation.test.tsx
git commit -m "feat(rules-react): useCatalog and useEvaluation hooks"
```

---

## Task 5: `RuleTree` headless component

**Files:**
- Create: `ui/packages/rules-react/src/RuleTree.tsx`
- Test: `ui/packages/rules-react/test/RuleTree.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-react/test/RuleTree.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { createElement } from 'react';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '../src/context.js';
import { RuleTree } from '../src/RuleTree.js';

describe('RuleTree', () => {
  it('renders a treeitem per node with ARIA level from depth', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { not: { spec: 'b' } }] } });

    render(
      createElement(RuleEditorProvider, {
        store,
        children: createElement(RuleTree, {
          children: (item) =>
            createElement('span', { key: item.path, 'data-path': item.path, 'data-level': item.level }, item.path),
        }),
      }),
    );

    // role="tree" wrapper, one treeitem per node (4 nodes: root, and[0], and[1], and[1].not)
    expect(screen.getByRole('tree')).toBeDefined();
    const items = screen.getAllByRole('treeitem');
    expect(items).toHaveLength(4);
    expect(items[0]!.getAttribute('aria-level')).toBe('1');

    // deepest node ($.rule.and[1].not) has level 3
    const deepest = items.find((el) => el.textContent === '$.rule.and[1].not');
    expect(deepest!.getAttribute('aria-level')).toBe('3');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-react test test/RuleTree.test.tsx
```
Expected: FAILS — `../src/RuleTree.js` does not exist.

- [ ] **Step 3: Write the component**

`ui/packages/rules-react/src/RuleTree.tsx`:
```tsx
import { errorsForNode, listPaths, type RuleError, type RuleNode } from '@motiv/rules-core';
import type { ReactNode } from 'react';
import { useRuleEditorStore } from './context.js';
import { useRuleEditor } from './useRuleEditor.js';

/** One node surfaced to a {@link RuleTree} render prop. */
export interface RuleTreeItem {
  path: string;
  node: RuleNode;
  /** 1-based ARIA level (root = 1). */
  level: number;
  errors: RuleError[];
}

const ROOT = '$.rule';

function depthOf(path: string): number {
  if (path === ROOT) return 0;
  return path.slice(ROOT.length).split('.').filter(Boolean).length;
}

/**
 * Headless rule tree: flattens the current document (pre-order) and wraps each node in an
 * ARIA treeitem, delegating all visible markup to the render-prop `children`.
 */
export function RuleTree(props: { children: (item: RuleTreeItem) => ReactNode }): ReactNode {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    <div role="tree">
      {listPaths(state.document).map(({ path, node }) => (
        <div key={path} role="treeitem" aria-level={depthOf(path) + 1}>
          {props.children({ path, node, level: depthOf(path) + 1, errors: errorsForNode(state.errors, path) })}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-react test test/RuleTree.test.tsx
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-react/src/RuleTree.tsx ui/packages/rules-react/test/RuleTree.test.tsx
git commit -m "feat(rules-react): headless RuleTree component"
```

---

## Task 6: `JustificationTree` headless component

**Files:**
- Create: `ui/packages/rules-react/src/JustificationTree.tsx`
- Test: `ui/packages/rules-react/test/JustificationTree.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-react/test/JustificationTree.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { createElement } from 'react';
import type { ExplanationNode } from '@motiv/rules-core';
import { JustificationTree } from '../src/JustificationTree.js';

const explanation: ExplanationNode = {
  assertions: ['AND'],
  underlying: [
    { assertions: ['is positive'], underlying: [] },
    { assertions: ['is even'], underlying: [{ assertions: ['divisible by 2'], underlying: [] }] },
  ],
};

describe('JustificationTree', () => {
  it('renders every row and collapses a subtree on toggle', () => {
    render(
      createElement(JustificationTree, {
        explanation,
        children: ({ row, toggle }) =>
          createElement(
            'button',
            { key: row.id, 'data-id': row.id, 'aria-level': row.depth + 1, onClick: () => toggle(row.id) },
            row.assertions.join(', '),
          ),
      }),
    );

    // 4 rows initially: AND, is positive, is even, divisible by 2
    expect(screen.getAllByRole('treeitem')).toHaveLength(4);

    // collapse the "is even" subtree (id '0.1') → its child 'divisible by 2' disappears
    fireEvent.click(screen.getByText('is even'));
    expect(screen.getAllByRole('treeitem')).toHaveLength(3);
    expect(screen.queryByText('divisible by 2')).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-react test test/JustificationTree.test.tsx
```
Expected: FAILS — `../src/JustificationTree.js` does not exist.

- [ ] **Step 3: Write the component**

`ui/packages/rules-react/src/JustificationTree.tsx`:
```tsx
import {
  flattenExplanation, toExplanationView,
  type ExplanationNode, type ExplanationRow,
} from '@motiv/rules-core';
import { useMemo, useState, type ReactNode } from 'react';

/** A row surfaced to a {@link JustificationTree} render prop, plus a collapse toggle. */
export interface JustificationRow {
  row: ExplanationRow;
  toggle: (id: string) => void;
}

/**
 * Headless explanation tree: derives collapsible rows from an explanation and wraps each in an
 * ARIA treeitem, delegating visible markup to the render-prop `children`. Collapse state is
 * owned internally.
 */
export function JustificationTree(props: {
  explanation: ExplanationNode;
  children: (row: JustificationRow) => ReactNode;
}): ReactNode {
  const view = useMemo(() => toExplanationView(props.explanation), [props.explanation]);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(() => new Set());

  const toggle = (id: string): void =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const rows = flattenExplanation(view, collapsed);

  return (
    <div role="tree">
      {rows.map((row) => (
        <div
          key={row.id}
          role="treeitem"
          aria-level={row.depth + 1}
          aria-expanded={row.hasChildren ? !row.collapsed : undefined}
        >
          {props.children({ row, toggle })}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-react test test/JustificationTree.test.tsx
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-react/src/JustificationTree.tsx ui/packages/rules-react/test/JustificationTree.test.tsx
git commit -m "feat(rules-react): headless JustificationTree component"
```

---

## Task 7: Public barrel, whole-package verification

**Files:**
- Modify: `ui/packages/rules-react/src/index.ts`

- [ ] **Step 1: Write the barrel**

Replace `ui/packages/rules-react/src/index.ts` with:
```ts
export { RuleEditorProvider, useRuleEditorStore } from './context.js';
export { useRuleEditor } from './useRuleEditor.js';
export { useRuleNode, type RuleNodeView } from './useRuleNode.js';
export { useCatalog, type CatalogState } from './useCatalog.js';
export { useEvaluation, type EvaluationState } from './useEvaluation.js';
export { RuleTree, type RuleTreeItem } from './RuleTree.js';
export { JustificationTree, type JustificationRow } from './JustificationTree.js';
```

- [ ] **Step 2: Verify the whole package typechecks, builds, and tests green**

Run (from `ui/`):
```bash
pnpm -C packages/rules-react typecheck
pnpm -C packages/rules-react build
pnpm -C packages/rules-react test
```
Expected: typecheck clean; `dist/` emitted with `index.d.ts`; all seven suites (smoke + six feature suites) pass. Fix any barrel/type errors before continuing.

- [ ] **Step 3: Confirm the workspace CI still covers the new package**

The `ui.yml` workflow from Plan 3 runs `pnpm -r typecheck/build/test`, which now includes `@motiv/rules-react` automatically — no workflow change needed. Confirm both UI packages are picked up:
```bash
pnpm -r exec node -e "console.log(process.env.PNPM_PACKAGE_NAME ?? require('./package.json').name)"
```
Expected output includes both `@motiv/rules-core` and `@motiv/rules-react`.

- [ ] **Step 4: Commit**

```bash
git add ui/packages/rules-react/src/index.ts
git commit -m "feat(rules-react): public barrel export"
```

---

## Task 8: Mandatory simplification review

**Files:**
- Review: everything under `ui/packages/rules-react/src/`.

Per CLAUDE.md, a `code-simplifier` review is required after implementation.

- [ ] **Step 1: Spawn the code-simplifier agent**

Dispatch `code-simplifier:code-simplifier` over the `ui/packages/rules-react/src/` files. Instruct it to preserve the public API (the barrel's exports), the headless (no-styling) constraint, the `react` peer-dependency boundary, and the snapshot-cache in `useRuleEditor` (it is load-bearing, not redundant) — and to focus on duplication, convoluted design, and naming.

- [ ] **Step 2: Apply accepted suggestions and re-run**

If it proposes changes, apply them, then run (from `ui/`):
```bash
pnpm -C packages/rules-react typecheck && pnpm -C packages/rules-react test
```
Expected: clean typecheck; all tests pass.

- [ ] **Step 3: Commit any changes**

```bash
git add ui/packages/rules-react/src/
git commit -m "refactor(rules-react): simplify per review"
```

(Skip if nothing changed.)

---

## Self-Review Notes (author)

- **Spec coverage (design §Frontend → `@motiv/rules-react`):**
  - *Hooks `useRuleEditor(store)`, `useRuleNode(path)`, `useCatalog(client)`, `useEvaluation(client)`* → Tasks 2, 3, 4.
  - *Headless components `<RuleEditorProvider>`, `<RuleTree>`/`<RuleNodeSlot>` (render-prop), `<JustificationTree>`* → Task 2 (provider), 5 (`RuleTree` — the render-prop subsumes a separate `RuleNodeSlot`; a distinct slot component would be redundant indirection, so it is folded in — flagged, not silently dropped), 6 (`JustificationTree`).
  - *No CSS shipped; styling is the consumer's* → all components are headless render-prop wrappers emitting only ARIA structure.
- **Deferred by design (Plan 5):** the demo app, the sample ASP.NET host, and the Playwright E2E — the vertical-slice integration proof. This plan delivers only the reusable library.
- **Load-bearing detail:** the snapshot cache in `useRuleEditor` (Task 2) is required because `RuleEditorStore.getState()` returns a fresh object per call; without caching, `useSyncExternalStore` loops. The stable-reference test asserts it.
- **Placeholder scan:** none — every step has concrete code/commands.
- **Type/name consistency:** `RuleEditorProvider`/`useRuleEditorStore`, `useRuleEditor`, `useRuleNode`/`RuleNodeView`, `useCatalog`/`CatalogState`, `useEvaluation`/`EvaluationState`, `RuleTree`/`RuleTreeItem`, `JustificationTree`/`JustificationRow` are used identically across tasks and the barrel. All core imports (`RuleEditorStore`, `EditorState`, `getNode`, `listPaths`, `errorsForNode`, `toExplanationView`, `flattenExplanation`, `ExplanationRow`, `CatalogEntry`, `EvaluationResult`, `EvaluateRequest`, `ExplanationNode`, `RuleError`, `RuleNode`, `RulesApiClient`) match `@motiv/rules-core`'s committed public surface.
- **Environment risk:** requires Node/pnpm (provisioned). React 18 peer; tests run under jsdom.
