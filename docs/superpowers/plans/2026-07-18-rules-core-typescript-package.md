# @motiv/rules-core (Headless TypeScript Core) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `@motiv/rules-core` — a zero-runtime-dependency, framework-free TypeScript package that gives a frontend everything it needs to browse, edit, validate, and explain Motiv rules: a typed document model mirroring `rule.v1.json`, a transport-injectable API client for the `rules-api.v1` contract, an observable rule-editor store with undo/redo and path-indexed validation, and a render-friendly explanation model.

**Architecture:** A pnpm workspace at `ui/` holding one package for now (`packages/rules-core`). The package is split into focused modules — `document` (rule types + guards), `contracts` (API DTOs), `client` (fetch-injectable `RulesApiClient`), `paths` (immutable tree addressing that mirrors the backend's `$.rule…` JSON paths), `editor` (a small subscribe/getState store), `validation` (a debounced controller wiring the store to the client), and `explanation` (a collapsible view model). Everything is synchronous and unit-testable except the one debounce controller, which is tested with fake timers.

**Tech Stack:** TypeScript 5, Vitest (tests), tsup (ESM+CJS+d.ts build), Ajv (dev-only, for the schema-drift test). Node ≥20 + pnpm ≥9.

---

## Context for the implementer (read first)

This is **Plan 3** of the rules-engine-frontend initiative. Plans 1–2 (merged on this branch) built the .NET side: `Motiv.Serialization` (rule loading, `ResultSerializer`) and `Motiv.Serialization.AspNetCore` (the `catalog`/`validate`/`evaluate` endpoints). The contracts this package mirrors are already committed:
- `schemas/rule.v1.json` — the rule-document grammar.
- `schemas/rules-api.v1.yaml` — the HTTP contract (catalog/validate/evaluate + DTOs).
Read both before starting. The frontend design spec is `docs/superpowers/specs/2026-07-16-rules-engine-frontend-ui-design.md` (§Frontend → `@motiv/rules-core`).

### ⚠️ Environment prerequisite
This package needs **Node.js ≥20 and pnpm ≥9** on PATH. Every build/test step runs `pnpm`. If `node`/`pnpm` are absent, install them before starting (e.g. `corepack enable && corepack prepare pnpm@9 --activate` on a machine with Node, or your platform's Node installer) — there is no way to run the TDD loop without them. All `pnpm` commands below run **from the `ui/` directory** unless stated otherwise.

### Scope (matches the Plan-1 load surface)
The **editor mutation ops** target the boolean subset the backend can load today: registry-`spec` leaves; `not`; the binary operators `and`/`or`/`xor`/`andAlso`/`orElse`; and `whenTrue`/`whenFalse`/`name` decoration. The **document types** and **path traversal**, however, model the *entire* `rule.v1.json` grammar (including `expression` and the higher-order `as…Satisfied` nodes) so the schema-drift test is meaningful and later plans need no retype — only the editing verbs are scoped. Do not build editor verbs for expression/higher-order nodes in this plan.

### Design decisions (do not deviate without escalating)
- **Zero runtime dependencies.** `package.json` `dependencies` must stay empty. Ajv/tsup/vitest/typescript are `devDependencies` only.
- **Node paths are strings that match the backend exactly**: `$.rule`, `$.rule.and[0]`, `$.rule.not`, `$.rule.andAlso[2]`, `$.rule.asAllSatisfied`. This makes mapping a `RuleError.path` to a node a direct string operation — no second path representation.
- **Immutable edits via `structuredClone`.** Every editor op produces a new `RuleDocument`; the previous reference is pushed onto the undo stack. Documents are small, so whole-tree clone is fine and obviously correct.
- **The store is synchronous.** Debounced auto-validation lives in a separate `validation` controller so the store stays timer-free and trivially testable.

### File structure (all under `ui/`)
```
ui/
├── package.json                      (private workspace root)
├── pnpm-workspace.yaml
├── tsconfig.base.json
└── packages/rules-core/
    ├── package.json                  (@motiv/rules-core, zero deps)
    ├── tsconfig.json
    ├── tsup.config.ts
    ├── src/
    │   ├── index.ts                  (barrel)
    │   ├── document.ts               (rule types, guards, nodeKind)
    │   ├── contracts.ts              (API DTOs)
    │   ├── client.ts                 (RulesApiClient, RulesApiError)
    │   ├── paths.ts                  (getNode/setNode/listPaths + parsing)
    │   ├── editor.ts                 (RuleEditorStore, errorsForNode)
    │   ├── validation.ts             (createValidationController)
    │   └── explanation.ts            (toExplanationView, flattenExplanation)
    └── test/
        ├── document.test.ts
        ├── schema.test.ts
        ├── client.test.ts
        ├── paths.test.ts
        ├── editor.test.ts
        ├── validation.test.ts
        └── explanation.test.ts
.github/workflows/ui.yml             (Node CI job)
```

### Test commands (from `ui/`)
Per-file during TDD: `pnpm -C packages/rules-core test test/<name>.test.ts` (the `test` script is `vitest run`, which takes a path filter).
Whole package: `pnpm -C packages/rules-core test`
Typecheck + build: `pnpm -C packages/rules-core typecheck && pnpm -C packages/rules-core build`

---

## Task 1: Workspace + package scaffold

**Files:**
- Create: `ui/package.json`, `ui/pnpm-workspace.yaml`, `ui/tsconfig.base.json`, `ui/.gitignore`
- Create: `ui/packages/rules-core/package.json`, `ui/packages/rules-core/tsconfig.json`, `ui/packages/rules-core/tsup.config.ts`, `ui/packages/rules-core/src/index.ts`, `ui/packages/rules-core/test/smoke.test.ts`

- [ ] **Step 1: Create the workspace root files**

`ui/package.json`:
```json
{
  "name": "motiv-ui",
  "private": true,
  "packageManager": "pnpm@9.12.0",
  "devDependencies": {
    "typescript": "^5.7.2"
  }
}
```

`ui/pnpm-workspace.yaml`:
```yaml
packages:
  - 'packages/*'
  - 'apps/*'
```

`ui/tsconfig.base.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "lib": ["ES2022", "DOM"],
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "declaration": true,
    "skipLibCheck": true,
    "verbatimModuleSyntax": true,
    "isolatedModules": true,
    "types": []
  }
}
```

`ui/.gitignore`:
```gitignore
node_modules/
dist/
*.tsbuildinfo
```

- [ ] **Step 2: Create the package files**

`ui/packages/rules-core/package.json`:
```json
{
  "name": "@motiv/rules-core",
  "version": "0.1.0",
  "description": "Headless, framework-free core for building Motiv rules-engine UIs.",
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
  "devDependencies": {
    "ajv": "^8.17.1",
    "tsup": "^8.3.5",
    "typescript": "^5.7.2",
    "vitest": "^2.1.8"
  }
}
```

`ui/packages/rules-core/tsconfig.json`:
```json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "outDir": "dist",
    "rootDir": ".",
    "types": ["node"]
  },
  "include": ["src", "test", "tsup.config.ts"]
}
```

`ui/packages/rules-core/tsup.config.ts`:
```ts
import { defineConfig } from 'tsup';

export default defineConfig({
  entry: ['src/index.ts'],
  format: ['esm', 'cjs'],
  dts: true,
  clean: true,
  sourcemap: true,
});
```

`ui/packages/rules-core/src/index.ts`:
```ts
export {};
```

`ui/packages/rules-core/test/smoke.test.ts`:
```ts
import { describe, it, expect } from 'vitest';

describe('workspace', () => {
  it('runs vitest', () => {
    expect(1 + 1).toBe(2);
  });
});
```

`@types/node` is needed for `structuredClone`/`fetch` typings and the schema test's `fs`/`url` imports; the next step adds it.

- [ ] **Step 3: Install and verify the toolchain**

Run (from `ui/`):
```bash
pnpm add -D -w typescript
pnpm -C packages/rules-core add -D @types/node ajv tsup typescript vitest
pnpm install
pnpm -C packages/rules-core test
```
Expected: `pnpm install` writes `ui/pnpm-lock.yaml`; the smoke test passes (`1 passed`). If `pnpm` is not found, STOP — see the environment prerequisite above.

- [ ] **Step 4: Verify build + typecheck work**

Run:
```bash
pnpm -C packages/rules-core typecheck
pnpm -C packages/rules-core build
```
Expected: typecheck clean; `dist/index.js`, `dist/index.cjs`, `dist/index.d.ts` produced.

- [ ] **Step 5: Commit**

```bash
git add ui/
git commit -m "feat(ui): scaffold pnpm workspace and rules-core package"
```

---

## Task 2: Rule document types, guards, and nodeKind

**Files:**
- Create: `ui/packages/rules-core/src/document.ts`
- Test: `ui/packages/rules-core/test/document.test.ts`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-core/test/document.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import {
  nodeKind,
  isSpecNode,
  isNotNode,
  isBinaryNode,
  binaryOperator,
  operandsOf,
  type RuleNode,
} from '../src/document.js';

describe('nodeKind', () => {
  it('identifies a spec leaf', () => {
    expect(nodeKind({ spec: 'is-positive' })).toBe('spec');
  });
  it('identifies each binary operator', () => {
    expect(nodeKind({ and: [{ spec: 'a' }, { spec: 'b' }] })).toBe('and');
    expect(nodeKind({ orElse: [{ spec: 'a' }, { spec: 'b' }] })).toBe('orElse');
  });
  it('identifies not and higher-order nodes', () => {
    expect(nodeKind({ not: { spec: 'a' } })).toBe('not');
    expect(nodeKind({ asAllSatisfied: { spec: 'a' } })).toBe('asAllSatisfied');
  });
});

describe('guards', () => {
  const and: RuleNode = { and: [{ spec: 'a' }, { spec: 'b' }] };
  it('narrows spec, not, and binary nodes', () => {
    expect(isSpecNode({ spec: 'a' })).toBe(true);
    expect(isSpecNode(and)).toBe(false);
    expect(isNotNode({ not: { spec: 'a' } })).toBe(true);
    expect(isBinaryNode(and)).toBe(true);
    expect(isBinaryNode({ spec: 'a' })).toBe(false);
  });
  it('exposes the binary operator and operands', () => {
    expect(binaryOperator(and)).toBe('and');
    expect(operandsOf(and)).toHaveLength(2);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-core test test/document.test.ts
```
Expected: FAILS — `../src/document.js` does not exist.

- [ ] **Step 3: Write the implementation**

`ui/packages/rules-core/src/document.ts`:
```ts
/** A whenTrue/whenFalse payload: an explanation string or a metadata object. */
export type Payload = string | Record<string, unknown>;

/** Optional decoration any node may carry. */
export interface Decoration {
  whenTrue?: Payload;
  whenFalse?: Payload;
  name?: string;
}

/** An N value: a literal count or a "@param" reference. */
export type Countable = number | string;

export interface SpecNode extends Decoration { spec: string }
export interface ExpressionNode extends Decoration { expression: string }
export interface NotNode extends Decoration { not: RuleNode }
export interface AndNode extends Decoration { and: RuleNode[] }
export interface OrNode extends Decoration { or: RuleNode[] }
export interface XorNode extends Decoration { xor: RuleNode[] }
export interface AndAlsoNode extends Decoration { andAlso: RuleNode[] }
export interface OrElseNode extends Decoration { orElse: RuleNode[] }
export interface AsAllSatisfiedNode extends Decoration { asAllSatisfied: RuleNode; path?: string }
export interface AsAnySatisfiedNode extends Decoration { asAnySatisfied: RuleNode; path?: string }
export interface AsNSatisfiedNode extends Decoration { asNSatisfied: RuleNode; n: Countable; path?: string }
export interface AsAtLeastNSatisfiedNode extends Decoration { asAtLeastNSatisfied: RuleNode; n: Countable; path?: string }
export interface AsAtMostNSatisfiedNode extends Decoration { asAtMostNSatisfied: RuleNode; n: Countable; path?: string }

export type BinaryNode = AndNode | OrNode | XorNode | AndAlsoNode | OrElseNode;
export type HigherOrderNode =
  | AsAllSatisfiedNode | AsAnySatisfiedNode | AsNSatisfiedNode
  | AsAtLeastNSatisfiedNode | AsAtMostNSatisfiedNode;

export type RuleNode =
  | SpecNode | ExpressionNode | NotNode | BinaryNode | HigherOrderNode;

/** A parameter declaration in a rule document. */
export interface ParameterDeclaration {
  type: 'integer' | 'number' | 'string' | 'boolean';
  default?: number | string | boolean;
}

/** A complete externalized Motiv rule document. */
export interface RuleDocument {
  $schema?: string;
  name?: string;
  parameters?: Record<string, ParameterDeclaration>;
  rule: RuleNode;
}

export const BINARY_OPERATORS = ['and', 'or', 'xor', 'andAlso', 'orElse'] as const;
export type BinaryOperator = (typeof BINARY_OPERATORS)[number];

const HIGHER_ORDER_KEYS = [
  'asAllSatisfied', 'asAnySatisfied', 'asNSatisfied',
  'asAtLeastNSatisfied', 'asAtMostNSatisfied',
] as const;
type HigherOrderKey = (typeof HIGHER_ORDER_KEYS)[number];

/** The discriminant of a node: which operator or leaf it is. */
export type NodeKind = 'spec' | 'expression' | 'not' | BinaryOperator | HigherOrderKey;

const KIND_ORDER: readonly NodeKind[] = [
  'spec', 'expression', 'not', ...BINARY_OPERATORS, ...HIGHER_ORDER_KEYS,
];

/** Returns the discriminant key of a node. */
export function nodeKind(node: RuleNode): NodeKind {
  for (const key of KIND_ORDER) {
    if (key in node) return key;
  }
  throw new Error(`Unrecognised rule node: ${JSON.stringify(node)}`);
}

export function isSpecNode(node: RuleNode): node is SpecNode { return 'spec' in node; }
export function isExpressionNode(node: RuleNode): node is ExpressionNode { return 'expression' in node; }
export function isNotNode(node: RuleNode): node is NotNode { return 'not' in node; }

export function isBinaryNode(node: RuleNode): node is BinaryNode {
  return BINARY_OPERATORS.some((op) => op in node);
}

export function isHigherOrderNode(node: RuleNode): node is HigherOrderNode {
  return HIGHER_ORDER_KEYS.some((key) => key in node);
}

/** The operator of a binary node. */
export function binaryOperator(node: BinaryNode): BinaryOperator {
  const op = BINARY_OPERATORS.find((candidate) => candidate in node);
  if (!op) throw new Error('Node is not a binary operator node.');
  return op;
}

/** The operands array of a binary node. */
export function operandsOf(node: BinaryNode): RuleNode[] {
  return (node as Record<BinaryOperator, RuleNode[]>)[binaryOperator(node)];
}

/** The single child key of a higher-order node. */
export function higherOrderKey(node: HigherOrderNode): HigherOrderKey {
  const key = HIGHER_ORDER_KEYS.find((candidate) => candidate in node);
  if (!key) throw new Error('Node is not a higher-order node.');
  return key;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/document.test.ts
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/document.ts ui/packages/rules-core/test/document.test.ts
git commit -m "feat(rules-core): rule document types, guards, and nodeKind"
```

---

## Task 3: Schema-drift test

**Files:**
- Test: `ui/packages/rules-core/test/schema.test.ts`

Guards against the TS types silently drifting from `schemas/rule.v1.json`: representative documents built through the TS types must validate against the shipped schema.

- [ ] **Step 1: Write the test**

`ui/packages/rules-core/test/schema.test.ts`:
```ts
import { describe, it, expect, beforeAll } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import Ajv2020, { type ValidateFunction } from 'ajv/dist/2020.js';
import type { RuleDocument } from '../src/document.js';

const schemaPath = fileURLToPath(
  new URL('../../../../schemas/rule.v1.json', import.meta.url),
);

let validate: ValidateFunction;

beforeAll(() => {
  const schema = JSON.parse(readFileSync(schemaPath, 'utf8'));
  validate = new Ajv2020({ strict: false }).compile(schema);
});

const documents: RuleDocument[] = [
  { rule: { spec: 'is-positive' } },
  { rule: { spec: 'is-positive', whenTrue: 'ok', whenFalse: 'bad' } },
  { rule: { spec: 'is-positive', whenTrue: 'ok', whenFalse: 'bad', name: 'check' } },
  { rule: { not: { spec: 'is-positive' } } },
  { rule: { and: [{ spec: 'a' }, { spec: 'b' }] } },
  { rule: { orElse: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] } },
  { name: 'doc', rule: { xor: [{ spec: 'a' }, { not: { spec: 'b' } }] } },
];

describe('rule.v1.json drift', () => {
  it.each(documents)('accepts a well-formed document (%#)', (document) => {
    const ok = validate(document);
    expect(validate.errors ?? []).toEqual([]);
    expect(ok).toBe(true);
  });

  it('rejects a malformed document (mixed payload kinds)', () => {
    const bad = { rule: { spec: 'a', whenTrue: 'string', whenFalse: { obj: true } } };
    expect(validate(bad)).toBe(false);
  });
});
```

- [ ] **Step 2: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/schema.test.ts
```
Expected: PASS (all documents valid, the malformed one rejected). If the relative path to `schemas/rule.v1.json` is wrong the `beforeAll` will throw — confirm the four `../` segments reach the repo root from `ui/packages/rules-core/test/`.

- [ ] **Step 3: Commit**

```bash
git add ui/packages/rules-core/test/schema.test.ts
git commit -m "test(rules-core): schema-drift guard against rule.v1.json"
```

---

## Task 4: Contract types + RulesApiClient

**Files:**
- Create: `ui/packages/rules-core/src/contracts.ts`
- Create: `ui/packages/rules-core/src/client.ts`
- Test: `ui/packages/rules-core/test/client.test.ts`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-core/test/client.test.ts`:
```ts
import { describe, it, expect, vi } from 'vitest';
import { RulesApiClient, RulesApiError } from '../src/client.js';
import type { CatalogEntry, EvaluationResult, ValidationResponse } from '../src/contracts.js';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('RulesApiClient', () => {
  it('fetches the catalog from {baseUrl}/catalog', async () => {
    const entries: CatalogEntry[] = [
      { name: 'is-positive', modelType: 'number', metadataType: 'String', isAsync: false, description: null },
    ];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(entries));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.getCatalog();

    expect(fetchMock).toHaveBeenCalledWith('/api/rules/catalog', expect.objectContaining({ method: 'GET' }));
    expect(result).toEqual(entries);
  });

  it('posts a validate request and returns the errors', async () => {
    const body: ValidationResponse = { errors: [] };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.validate({ modelType: 'number', document: { rule: { spec: 'is-positive' } } });

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/rules/validate');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ modelType: 'number', document: { rule: { spec: 'is-positive' } } });
    expect(result).toEqual(body);
  });

  it('returns the evaluation result on success', async () => {
    const body: EvaluationResult = {
      satisfied: true, reason: 'is positive', assertions: ['is positive'],
      values: ['is positive'], justification: 'is positive',
      explanation: { assertions: ['is positive'], underlying: [] },
    };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.evaluate({ modelType: 'number', document: { rule: { spec: 'is-positive' } }, model: 5 });

    expect(result.satisfied).toBe(true);
  });

  it('throws RulesApiError carrying validation errors on a 400', async () => {
    const body: ValidationResponse = { errors: [{ path: '$.rule', code: 'UnknownSpec', message: 'nope' }] };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body, 400));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    await expect(client.evaluate({ modelType: 'number', document: { rule: { spec: 'x' } }, model: 5 }))
      .rejects.toMatchObject({ status: 400, errors: body.errors });
    await expect(client.evaluate({ modelType: 'number', document: { rule: { spec: 'x' } }, model: 5 }))
      .rejects.toBeInstanceOf(RulesApiError);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-core test test/client.test.ts
```
Expected: FAILS — `../src/client.js` / `../src/contracts.js` do not exist.

- [ ] **Step 3: Write the contracts**

`ui/packages/rules-core/src/contracts.ts`:
```ts
import type { RuleDocument } from './document.js';

/** One catalog listing for a registered specification. */
export interface CatalogEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  description?: string | null;
}

/** Stable machine-readable rule-document error codes. */
export type RuleErrorCode =
  | 'InvalidNode' | 'UnknownSpec' | 'ModelTypeMismatch' | 'MetadataTypeMismatch'
  | 'MixedWhenTrueFalseKinds' | 'ExpressionsNotEnabled' | 'AsyncSpecInSyncLoad' | 'DocumentTooLarge';

/** A single validation or load error. */
export interface RuleError {
  path: string;
  code: RuleErrorCode;
  message: string;
}

/** The body returned by the validate endpoint (and by evaluate on an invalid document). */
export interface ValidationResponse {
  errors: RuleError[];
}

/** A request-level error envelope (e.g. unknown model type). */
export interface ErrorResponse {
  error: string;
}

/** A node in the de-noised causal explanation tree. */
export interface ExplanationNode {
  assertions: string[];
  underlying: ExplanationNode[];
}

/** The serialized outcome of an evaluation. */
export interface EvaluationResult {
  satisfied: boolean;
  reason: string;
  assertions: string[];
  values: string[];
  justification: string;
  explanation: ExplanationNode;
}

/** Request body for the validate endpoint. */
export interface ValidateRequest {
  modelType: string;
  document: RuleDocument;
}

/** Request body for the evaluate endpoint. */
export interface EvaluateRequest {
  modelType: string;
  document: RuleDocument;
  model: unknown;
}
```

- [ ] **Step 4: Write the client**

`ui/packages/rules-core/src/client.ts`:
```ts
import type {
  CatalogEntry, ErrorResponse, EvaluateRequest, EvaluationResult,
  RuleError, ValidateRequest, ValidationResponse,
} from './contracts.js';

/** Options for constructing a {@link RulesApiClient}. */
export interface RulesApiClientOptions {
  /** Base path the API is mounted under, e.g. "/api/rules". No trailing slash. */
  baseUrl: string;
  /** Injectable fetch implementation; defaults to the global fetch. */
  fetch?: typeof fetch;
}

/** Thrown when the API returns a non-2xx response. */
export class RulesApiError extends Error {
  readonly status: number;
  /** Present when the failure body was a ValidationResponse. */
  readonly errors?: RuleError[];

  constructor(status: number, message: string, errors?: RuleError[]) {
    super(message);
    this.name = 'RulesApiError';
    this.status = status;
    if (errors) this.errors = errors;
  }
}

/** A transport-agnostic client for the Motiv rules API. */
export class RulesApiClient {
  readonly #baseUrl: string;
  readonly #fetch: typeof fetch;

  constructor(options: RulesApiClientOptions) {
    this.#baseUrl = options.baseUrl.replace(/\/$/, '');
    this.#fetch = options.fetch ?? globalThis.fetch;
  }

  /** GET {baseUrl}/catalog */
  async getCatalog(): Promise<CatalogEntry[]> {
    const response = await this.#fetch(`${this.#baseUrl}/catalog`, { method: 'GET' });
    return this.#read<CatalogEntry[]>(response);
  }

  /** POST {baseUrl}/validate */
  async validate(request: ValidateRequest): Promise<ValidationResponse> {
    const response = await this.#post('/validate', request);
    return this.#read<ValidationResponse>(response);
  }

  /** POST {baseUrl}/evaluate */
  async evaluate(request: EvaluateRequest): Promise<EvaluationResult> {
    const response = await this.#post('/evaluate', request);
    return this.#read<EvaluationResult>(response);
  }

  #post(path: string, body: unknown): Promise<Response> {
    return this.#fetch(`${this.#baseUrl}${path}`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  async #read<T>(response: Response): Promise<T> {
    if (response.ok) return (await response.json()) as T;
    const body = (await response.json().catch(() => undefined)) as
      | ValidationResponse | ErrorResponse | undefined;
    if (body && 'errors' in body) {
      throw new RulesApiError(response.status, `Request failed (${response.status}).`, body.errors);
    }
    const message = body && 'error' in body ? body.error : `Request failed (${response.status}).`;
    throw new RulesApiError(response.status, message);
  }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/client.test.ts
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-core/src/contracts.ts ui/packages/rules-core/src/client.ts ui/packages/rules-core/test/client.test.ts
git commit -m "feat(rules-core): API contract types and RulesApiClient"
```

---

## Task 5: Immutable path helpers

**Files:**
- Create: `ui/packages/rules-core/src/paths.ts`
- Test: `ui/packages/rules-core/test/paths.test.ts`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-core/test/paths.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { getNode, setNode, listPaths } from '../src/paths.js';
import type { RuleDocument } from '../src/document.js';

const doc: RuleDocument = {
  rule: { and: [{ spec: 'a' }, { not: { spec: 'b' } }] },
};

describe('listPaths', () => {
  it('emits backend-shaped paths for every node, root first', () => {
    expect(listPaths(doc).map((p) => p.path)).toEqual([
      '$.rule',
      '$.rule.and[0]',
      '$.rule.and[1]',
      '$.rule.and[1].not',
    ]);
  });
});

describe('getNode', () => {
  it('resolves the root and nested nodes', () => {
    expect(getNode(doc, '$.rule')).toBe(doc.rule);
    expect(getNode(doc, '$.rule.and[0]')).toEqual({ spec: 'a' });
    expect(getNode(doc, '$.rule.and[1].not')).toEqual({ spec: 'b' });
  });
  it('returns undefined for a missing path', () => {
    expect(getNode(doc, '$.rule.and[9]')).toBeUndefined();
  });
});

describe('setNode', () => {
  it('replaces a nested node without mutating the original', () => {
    const next = setNode(doc, '$.rule.and[0]', { spec: 'z' });
    expect(getNode(next, '$.rule.and[0]')).toEqual({ spec: 'z' });
    expect(getNode(doc, '$.rule.and[0]')).toEqual({ spec: 'a' });
    expect(next).not.toBe(doc);
  });
  it('replaces the root node', () => {
    const next = setNode(doc, '$.rule', { spec: 'only' });
    expect(next.rule).toEqual({ spec: 'only' });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-core test test/paths.test.ts
```
Expected: FAILS — `../src/paths.js` does not exist.

- [ ] **Step 3: Write the implementation**

`ui/packages/rules-core/src/paths.ts`:
```ts
import {
  binaryOperator, higherOrderKey, isBinaryNode, isHigherOrderNode, isNotNode,
  operandsOf, type RuleDocument, type RuleNode,
} from './document.js';

const ROOT = '$.rule';

interface Step { key: string; index?: number }

function parseSteps(path: string): Step[] {
  if (path !== ROOT && !path.startsWith(`${ROOT}.`)) {
    throw new Error(`Invalid node path: ${path}`);
  }
  const rest = path.slice(ROOT.length);
  if (rest === '') return [];
  return rest.split('.').filter(Boolean).map((token) => {
    const match = token.match(/^([A-Za-z]+)(?:\[(\d+)\])?$/);
    if (!match) throw new Error(`Invalid path token: ${token}`);
    return match[2] === undefined
      ? { key: match[1]! }
      : { key: match[1]!, index: Number(match[2]) };
  });
}

/** Rebuilds a path string from steps (inverse of parseSteps). */
export function joinSteps(basePath: string, ...appended: Step[]): string {
  return appended.reduce<string>(
    (acc, step) => (step.index === undefined ? `${acc}.${step.key}` : `${acc}.${step.key}[${step.index}]`),
    basePath,
  );
}

/** The parent path and final step of a non-root path (throws for the root). */
export function splitLast(path: string): { parentPath: string; step: Step } {
  const steps = parseSteps(path);
  const step = steps.at(-1);
  if (!step) throw new Error(`Path has no parent: ${path}`);
  return { parentPath: joinSteps(ROOT, ...steps.slice(0, -1)), step };
}

/** Resolves the node at a path, or undefined when it does not exist. */
export function getNode(document: RuleDocument, path: string): RuleNode | undefined {
  let node: RuleNode | undefined = document.rule;
  for (const { key, index } of parseSteps(path)) {
    if (!node) return undefined;
    const child = (node as Record<string, unknown>)[key];
    node = index === undefined
      ? (child as RuleNode | undefined)
      : (Array.isArray(child) ? (child[index] as RuleNode | undefined) : undefined);
  }
  return node;
}

/** Returns a new document with the node at a path replaced. */
export function setNode(document: RuleDocument, path: string, replacement: RuleNode): RuleDocument {
  const clone = structuredClone(document);
  const steps = parseSteps(path);
  if (steps.length === 0) {
    clone.rule = replacement;
    return clone;
  }
  let parent: Record<string, unknown> = clone.rule as unknown as Record<string, unknown>;
  for (const { key, index } of steps.slice(0, -1)) {
    const child = parent[key];
    parent = (index === undefined ? child : (child as unknown[])[index]) as Record<string, unknown>;
  }
  const last = steps.at(-1)!;
  if (last.index === undefined) parent[last.key] = replacement;
  else (parent[last.key] as RuleNode[])[last.index] = replacement;
  return clone;
}

/** Every node in the tree with its backend-shaped path, root first (pre-order). */
export function listPaths(document: RuleDocument): Array<{ path: string; node: RuleNode }> {
  const out: Array<{ path: string; node: RuleNode }> = [];
  const walk = (node: RuleNode, path: string): void => {
    out.push({ path, node });
    if (isNotNode(node)) {
      walk(node.not, `${path}.not`);
    } else if (isBinaryNode(node)) {
      const op = binaryOperator(node);
      operandsOf(node).forEach((child, i) => walk(child, `${path}.${op}[${i}]`));
    } else if (isHigherOrderNode(node)) {
      const key = higherOrderKey(node);
      walk((node as Record<string, RuleNode>)[key]!, `${path}.${key}`);
    }
  };
  walk(document.rule, ROOT);
  return out;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/paths.test.ts
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/paths.ts ui/packages/rules-core/test/paths.test.ts
git commit -m "feat(rules-core): immutable tree path helpers"
```

---

## Task 6: Rule editor store

**Files:**
- Create: `ui/packages/rules-core/src/editor.ts`
- Test: `ui/packages/rules-core/test/editor.test.ts`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-core/test/editor.test.ts`:
```ts
import { describe, it, expect, vi } from 'vitest';
import { RuleEditorStore, errorsForNode } from '../src/editor.js';
import { getNode } from '../src/paths.js';
import type { RuleDocument } from '../src/document.js';
import type { RuleError } from '../src/contracts.js';

const initial: RuleDocument = { rule: { spec: 'a' } };

describe('RuleEditorStore edits', () => {
  it('replaces a node and notifies subscribers', () => {
    const store = new RuleEditorStore(initial);
    const listener = vi.fn();
    store.subscribe(listener);

    store.replaceNode('$.rule', { spec: 'b' });

    expect(store.getState().document.rule).toEqual({ spec: 'b' });
    expect(listener).toHaveBeenCalledOnce();
  });

  it('wraps a node in a binary operator with a supplied sibling', () => {
    const store = new RuleEditorStore(initial);
    store.wrapInOperator('$.rule', 'and', { spec: 'b' });
    expect(store.getState().document.rule).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }] });
  });

  it('adds and removes operands, unwrapping when only one remains', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.addOperand('$.rule', { spec: 'c' });
    expect(getNode(store.getState().document, '$.rule')).toEqual({ and: [{ spec: 'a' }, { spec: 'b' }, { spec: 'c' }] });

    store.removeOperand('$.rule.and[2]');
    store.removeOperand('$.rule.and[1]');
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
  });

  it('unwraps an operator node to its first operand', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.unwrap('$.rule');
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
  });

  it('sets decoration and name', () => {
    const store = new RuleEditorStore(initial);
    store.setDecoration('$.rule', { whenTrue: 'yes', whenFalse: 'no' });
    store.setName('$.rule', 'my check');
    expect(store.getState().document.rule).toEqual({ spec: 'a', whenTrue: 'yes', whenFalse: 'no', name: 'my check' });
  });
});

describe('RuleEditorStore history', () => {
  it('undoes and redoes edits', () => {
    const store = new RuleEditorStore(initial);
    store.replaceNode('$.rule', { spec: 'b' });
    expect(store.getState().canUndo).toBe(true);

    store.undo();
    expect(store.getState().document.rule).toEqual({ spec: 'a' });
    expect(store.getState().canRedo).toBe(true);

    store.redo();
    expect(store.getState().document.rule).toEqual({ spec: 'b' });
  });
});

describe('errorsForNode', () => {
  const errors: RuleError[] = [
    { path: '$.rule.and[0]', code: 'UnknownSpec', message: 'x' },
    { path: '$.rule.and[0].whenTrue', code: 'MixedWhenTrueFalseKinds', message: 'y' },
    { path: '$.rule.and[1]', code: 'UnknownSpec', message: 'z' },
  ];
  it('matches a node and its decoration sub-paths', () => {
    expect(errorsForNode(errors, '$.rule.and[0]').map((e) => e.code))
      .toEqual(['UnknownSpec', 'MixedWhenTrueFalseKinds']);
  });
  it('stores errors on the state and surfaces them per node', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
    store.setErrors(errors);
    expect(store.getState().errors).toHaveLength(3);
    expect(errorsForNode(store.getState().errors, '$.rule.and[1]')).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-core test test/editor.test.ts
```
Expected: FAILS — `../src/editor.js` does not exist.

- [ ] **Step 3: Write the implementation**

`ui/packages/rules-core/src/editor.ts`:
```ts
import {
  binaryOperator, isBinaryNode, isNotNode, operandsOf,
  type BinaryOperator, type Decoration, type RuleDocument, type RuleNode,
} from './document.js';
import type { RuleError } from './contracts.js';
import { getNode, setNode, splitLast } from './paths.js';

/** The observable state of a rule editor. */
export interface EditorState {
  document: RuleDocument;
  errors: RuleError[];
  canUndo: boolean;
  canRedo: boolean;
}

/** Errors anchored on a node or any of its sub-field paths (e.g. whenTrue). */
export function errorsForNode(errors: RuleError[], path: string): RuleError[] {
  const prefix = `${path}.`;
  return errors.filter((error) => error.path === path || error.path.startsWith(prefix));
}

/** A synchronous, subscribable store over an immutable rule document. */
export class RuleEditorStore {
  #document: RuleDocument;
  #errors: RuleError[] = [];
  #undo: RuleDocument[] = [];
  #redo: RuleDocument[] = [];
  readonly #listeners = new Set<() => void>();

  constructor(initial: RuleDocument) {
    this.#document = structuredClone(initial);
  }

  getState(): EditorState {
    return {
      document: this.#document,
      errors: this.#errors,
      canUndo: this.#undo.length > 0,
      canRedo: this.#redo.length > 0,
    };
  }

  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  replaceNode(path: string, node: RuleNode): void {
    this.#commit(setNode(this.#document, path, node));
  }

  wrapInOperator(path: string, operator: BinaryOperator, sibling: RuleNode): void {
    const existing = getNode(this.#document, path);
    if (!existing) throw new Error(`No node at ${path}.`);
    this.#commit(setNode(this.#document, path, { [operator]: [existing, sibling] } as RuleNode));
  }

  addOperand(operatorPath: string, node: RuleNode): void {
    const target = getNode(this.#document, operatorPath);
    if (!target || !isBinaryNode(target)) throw new Error(`No operator node at ${operatorPath}.`);
    const op = binaryOperator(target);
    const next = { ...target, [op]: [...operandsOf(target), node] } as RuleNode;
    this.#commit(setNode(this.#document, operatorPath, next));
  }

  removeOperand(elementPath: string): void {
    const { parentPath, step } = splitLast(elementPath);
    if (step.index === undefined) throw new Error(`${elementPath} is not an operator-array element.`);
    const parent = getNode(this.#document, parentPath);
    if (!parent || !isBinaryNode(parent)) throw new Error(`No operator node at ${parentPath}.`);
    const op = binaryOperator(parent);
    const remaining = operandsOf(parent).filter((_, i) => i !== step.index);
    const replacement = remaining.length === 1 ? remaining[0]! : ({ ...parent, [op]: remaining } as RuleNode);
    this.#commit(setNode(this.#document, parentPath, replacement));
  }

  unwrap(path: string): void {
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);
    if (isNotNode(node)) return this.#commit(setNode(this.#document, path, node.not));
    if (isBinaryNode(node)) return this.#commit(setNode(this.#document, path, operandsOf(node)[0]!));
    throw new Error(`Node at ${path} cannot be unwrapped.`);
  }

  setDecoration(path: string, decoration: Partial<Pick<Decoration, 'whenTrue' | 'whenFalse'>>): void {
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);
    this.#commit(setNode(this.#document, path, { ...node, ...decoration }));
  }

  setName(path: string, name: string | undefined): void {
    const node = getNode(this.#document, path);
    if (!node) throw new Error(`No node at ${path}.`);
    const next = { ...node } as RuleNode & { name?: string };
    if (name === undefined) delete next.name;
    else next.name = name;
    this.#commit(setNode(this.#document, path, next));
  }

  setErrors(errors: RuleError[]): void {
    this.#errors = errors;
    this.#notify();
  }

  undo(): void {
    const previous = this.#undo.pop();
    if (!previous) return;
    this.#redo.push(this.#document);
    this.#document = previous;
    this.#notify();
  }

  redo(): void {
    const next = this.#redo.pop();
    if (!next) return;
    this.#undo.push(this.#document);
    this.#document = next;
    this.#notify();
  }

  #commit(next: RuleDocument): void {
    this.#undo.push(this.#document);
    this.#redo = [];
    this.#document = next;
    this.#notify();
  }

  #notify(): void {
    for (const listener of this.#listeners) listener();
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/editor.test.ts
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/editor.ts ui/packages/rules-core/test/editor.test.ts
git commit -m "feat(rules-core): rule editor store with undo/redo and error indexing"
```

---

## Task 7: Debounced validation controller

**Files:**
- Create: `ui/packages/rules-core/src/validation.ts`
- Test: `ui/packages/rules-core/test/validation.test.ts`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-core/test/validation.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { RuleEditorStore } from '../src/editor.js';
import { createValidationController } from '../src/validation.js';
import type { RulesApiClient } from '../src/client.js';
import type { ValidationResponse } from '../src/contracts.js';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

function fakeClient(response: ValidationResponse) {
  return { validate: vi.fn().mockResolvedValue(response) } as unknown as RulesApiClient;
}

describe('createValidationController', () => {
  it('debounces edits into a single validate call and pushes errors to the store', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const response: ValidationResponse = { errors: [{ path: '$.rule', code: 'UnknownSpec', message: 'x' }] };
    const client = fakeClient(response);
    const dispose = createValidationController(store, client, { modelType: 'number', debounceMs: 100 });

    store.replaceNode('$.rule', { spec: 'b' });
    store.replaceNode('$.rule', { spec: 'c' });
    expect(client.validate).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(100);

    expect(client.validate).toHaveBeenCalledTimes(1);
    expect(client.validate).toHaveBeenCalledWith({ modelType: 'number', document: store.getState().document });
    expect(store.getState().errors).toEqual(response.errors);
    dispose();
  });

  it('stops validating after dispose', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    const client = fakeClient({ errors: [] });
    const dispose = createValidationController(store, client, { modelType: 'number', debounceMs: 100 });

    dispose();
    store.replaceNode('$.rule', { spec: 'b' });
    await vi.advanceTimersByTimeAsync(100);

    expect(client.validate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-core test test/validation.test.ts
```
Expected: FAILS — `../src/validation.js` does not exist.

- [ ] **Step 3: Write the implementation**

`ui/packages/rules-core/src/validation.ts`:
```ts
import type { RulesApiClient } from './client.js';
import type { RuleEditorStore } from './editor.js';

/** Options for the debounced validation controller. */
export interface ValidationControllerOptions {
  /** The model type id to validate against. */
  modelType: string;
  /** Idle delay before validating after the last edit. Defaults to 300ms. */
  debounceMs?: number;
}

/**
 * Subscribes to a store and, after each burst of edits settles, validates the
 * current document and writes the returned errors back onto the store. Returns a
 * dispose function that unsubscribes and cancels any pending validation.
 */
export function createValidationController(
  store: RuleEditorStore,
  client: RulesApiClient,
  options: ValidationControllerOptions,
): () => void {
  const debounceMs = options.debounceMs ?? 300;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let lastDocument = store.getState().document;

  const run = (): void => {
    const { document } = store.getState();
    void client
      .validate({ modelType: options.modelType, document })
      .then((response) => store.setErrors(response.errors))
      .catch(() => { /* transport failures leave prior errors in place */ });
  };

  const unsubscribe = store.subscribe(() => {
    // Ignore notifications that are not document changes (e.g. setErrors).
    const { document } = store.getState();
    if (document === lastDocument) return;
    lastDocument = document;
    if (timer) clearTimeout(timer);
    timer = setTimeout(run, debounceMs);
  });

  return () => {
    if (timer) clearTimeout(timer);
    unsubscribe();
  };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/validation.test.ts
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/validation.ts ui/packages/rules-core/test/validation.test.ts
git commit -m "feat(rules-core): debounced validation controller"
```

---

## Task 8: Explanation view model

**Files:**
- Create: `ui/packages/rules-core/src/explanation.ts`
- Test: `ui/packages/rules-core/test/explanation.test.ts`

- [ ] **Step 1: Write the failing test**

`ui/packages/rules-core/test/explanation.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { toExplanationView, flattenExplanation } from '../src/explanation.js';
import type { ExplanationNode } from '../src/contracts.js';

const tree: ExplanationNode = {
  assertions: ['AND'],
  underlying: [
    { assertions: ['is positive'], underlying: [] },
    { assertions: ['is even'], underlying: [{ assertions: ['divisible by 2'], underlying: [] }] },
  ],
};

describe('toExplanationView', () => {
  it('assigns stable ids and depth', () => {
    const view = toExplanationView(tree);
    expect(view.id).toBe('0');
    expect(view.depth).toBe(0);
    expect(view.children[0]!.id).toBe('0.0');
    expect(view.children[1]!.children[0]!.id).toBe('0.1.0');
    expect(view.children[1]!.children[0]!.depth).toBe(2);
  });
});

describe('flattenExplanation', () => {
  it('lists all rows when nothing is collapsed', () => {
    const rows = flattenExplanation(toExplanationView(tree), new Set());
    expect(rows.map((r) => r.id)).toEqual(['0', '0.0', '0.1', '0.1.0']);
    expect(rows[1]!.hasChildren).toBe(false);
    expect(rows[2]!.hasChildren).toBe(true);
  });
  it('hides descendants of a collapsed node', () => {
    const rows = flattenExplanation(toExplanationView(tree), new Set(['0.1']));
    expect(rows.map((r) => r.id)).toEqual(['0', '0.0', '0.1']);
    expect(rows[2]!.collapsed).toBe(true);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C packages/rules-core test test/explanation.test.ts
```
Expected: FAILS — `../src/explanation.js` does not exist.

- [ ] **Step 3: Write the implementation**

`ui/packages/rules-core/src/explanation.ts`:
```ts
import type { ExplanationNode } from './contracts.js';

/** A causal-explanation node prepared for rendering. */
export interface ExplanationView {
  id: string;
  depth: number;
  assertions: string[];
  children: ExplanationView[];
}

/** One visible row of a (partially collapsed) explanation tree. */
export interface ExplanationRow {
  id: string;
  depth: number;
  assertions: string[];
  hasChildren: boolean;
  collapsed: boolean;
}

/** Converts a raw explanation tree into a view model with stable ids and depth. */
export function toExplanationView(root: ExplanationNode): ExplanationView {
  const build = (node: ExplanationNode, id: string, depth: number): ExplanationView => ({
    id,
    depth,
    assertions: node.assertions,
    children: node.underlying.map((child, i) => build(child, `${id}.${i}`, depth + 1)),
  });
  return build(root, '0', 0);
}

/** Flattens the view into visible rows, hiding descendants of collapsed ids. */
export function flattenExplanation(
  view: ExplanationView,
  collapsedIds: ReadonlySet<string>,
): ExplanationRow[] {
  const rows: ExplanationRow[] = [];
  const visit = (node: ExplanationView): void => {
    const collapsed = collapsedIds.has(node.id);
    rows.push({
      id: node.id,
      depth: node.depth,
      assertions: node.assertions,
      hasChildren: node.children.length > 0,
      collapsed,
    });
    if (!collapsed) node.children.forEach(visit);
  };
  visit(view);
  return rows;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C packages/rules-core test test/explanation.test.ts
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/explanation.ts ui/packages/rules-core/test/explanation.test.ts
git commit -m "feat(rules-core): collapsible explanation view model"
```

---

## Task 9: Public barrel, build, and CI

**Files:**
- Modify: `ui/packages/rules-core/src/index.ts`
- Create: `.github/workflows/ui.yml`

- [ ] **Step 1: Write the barrel**

Replace `ui/packages/rules-core/src/index.ts` with:
```ts
export * from './document.js';
export * from './contracts.js';
export * from './client.js';
export * from './paths.js';
export * from './editor.js';
export * from './validation.js';
export * from './explanation.js';
```

- [ ] **Step 2: Verify the whole package builds, typechecks, and tests green**

Run (from `ui/`):
```bash
pnpm -C packages/rules-core typecheck
pnpm -C packages/rules-core build
pnpm -C packages/rules-core test
```
Expected: typecheck clean; `dist/` emitted with `index.d.ts`; all tests across the seven suites pass. Fix any barrel/type errors before continuing.

- [ ] **Step 3: Add the CI workflow**

`.github/workflows/ui.yml`:
```yaml
name: ui

on:
  push:
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: ui
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
        with:
          version: 9
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: pnpm
          cache-dependency-path: ui/pnpm-lock.yaml
      - run: pnpm install --frozen-lockfile
      - run: pnpm -r typecheck
      - run: pnpm -r build
      - run: pnpm -r test
```

- [ ] **Step 4: Confirm the lockfile is committed**

Run:
```bash
git status --porcelain ui/pnpm-lock.yaml
```
Expected: the lockfile exists and is tracked (the CI job's `--frozen-lockfile` depends on it). If it is untracked, `git add ui/pnpm-lock.yaml`.

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-core/src/index.ts .github/workflows/ui.yml ui/pnpm-lock.yaml
git commit -m "feat(rules-core): public barrel export and UI CI workflow"
```

---

## Task 10: Mandatory simplification review

**Files:**
- Review: everything under `ui/packages/rules-core/src/`.

Per CLAUDE.md, a `code-simplifier` review is required after implementation.

- [ ] **Step 1: Spawn the code-simplifier agent**

Dispatch `code-simplifier:code-simplifier` over the `ui/packages/rules-core/src/` files. Instruct it to preserve the public API (the barrel's exports) and the zero-runtime-dependency constraint, and to focus on duplication, convoluted design, and naming — not to add features.

- [ ] **Step 2: Apply accepted suggestions and re-run**

If it proposes changes, apply them, then run:
```bash
pnpm -C packages/rules-core typecheck && pnpm -C packages/rules-core test
```
Expected: clean typecheck; all tests pass.

- [ ] **Step 3: Commit any changes**

```bash
git add ui/packages/rules-core/src/
git commit -m "refactor(rules-core): simplify per review"
```

(Skip if nothing changed.)

---

## Self-Review Notes (author)

- **Spec coverage (design §Frontend → `@motiv/rules-core`):**
  - *`types` module — RuleDocument/RuleNode union + catalog/error/result types; schema-drift test* → Tasks 2 (document types + guards + `nodeKind`), 3 (drift test), 4 (contract types).
  - *`client` — `RulesApiClient`, injectable fetch + base URL* → Task 4.
  - *`editor` — observable store; insert/remove/wrap/unwrap/set whenTrue/whenFalse/name; undo/redo; errors indexed by JSON path* → Tasks 5 (paths) + 6 (store + `errorsForNode`).
  - *`editor` — debounced `client.validate()` after each edit* → Task 7 (extracted into `validation` controller to keep the store synchronous — a decomposition improvement noted explicitly).
  - *`explanation` — render-friendly model with depth + expand/collapse helpers* → Task 8. (Per-node "satisfied flags" from the design are **not** modelled: the backend `ExplanationNode` DTO carries only `assertions`/`underlying`, no per-node satisfied flag — `satisfied` is a top-level `EvaluationResult` field. Flagged, not silently dropped.)
  - *Zero runtime dependencies; CI Node job* → Task 1 (empty `dependencies`), Task 9 (`.github/workflows/ui.yml`).
- **Deferred by design:** editor verbs for `expression`/higher-order nodes (types + traversal support them; editing them is later); the React adapter, demo app, and Playwright E2E (Plan 4); publishing config beyond `exports`/`files`.
- **Placeholder scan:** none — every step has concrete code/commands.
- **Type/name consistency:** `RuleDocument`, `RuleNode`, `nodeKind`, `BinaryOperator`, `isBinaryNode`/`binaryOperator`/`operandsOf`, `getNode`/`setNode`/`listPaths`/`splitLast`/`joinSteps`, `RuleEditorStore` (with `replaceNode`/`wrapInOperator`/`addOperand`/`removeOperand`/`unwrap`/`setDecoration`/`setName`/`undo`/`redo`/`setErrors`/`getState`/`subscribe`), `errorsForNode`, `createValidationController`, `RulesApiClient`/`RulesApiError`, `EvaluationResult`/`ExplanationNode`, `toExplanationView`/`flattenExplanation` are used identically across tasks and match the committed `schemas/rule.v1.json` and `schemas/rules-api.v1.yaml`. Node paths use the exact `$.rule…` format the ASP.NET `RuleError.Path` emits.
- **Environment risk:** the plan cannot run in an environment without Node/pnpm; this is called out at the top and is the one hard prerequisite for execution.
