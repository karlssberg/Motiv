# Demo App + Sample Host + E2E Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the whole rules-engine slice end-to-end: a small ASP.NET Core sample host that exposes `MapMotivRules` over a realistic `Customer` domain and serves a single-page demo, plus a three-pane React demo app (build a rule → see live JSON + validation errors → evaluate against a sample model and render the explanation) using `@motiv/rules-core` + `@motiv/rules-react`, verified by component tests and one Playwright smoke test.

**Architecture:** A new web project `src/examples/Motiv.RulesEngine.Sample` registers three `Customer` specs and calls `app.MapMotivRules("/api/rules", …)`, and also serves static SPA files with a fallback to `index.html`. A new Vite React app `ui/apps/demo` consumes the two workspace packages; it builds its static assets into the host's `wwwroot`, so one process serves both the API and the SPA (same origin, no CORS). Panes are thin: they wire the core's store/client and the adapter's hooks/components to minimal markup. Component tests use Testing Library with a mocked client; one Playwright test drives a real browser against the running host.

**Tech Stack:** ASP.NET Core (net10, `Microsoft.NET.Sdk.Web`), Vite + React 18 + TypeScript, Vitest + Testing Library (jsdom), Playwright. Node ≥20 + pnpm ≥9, .NET 10.

---

## Context for the implementer (read first)

This is **Plan 5** (final) of the rules-engine-frontend initiative. Plans 1–4 (merged on this branch) built: result serialization + the `catalog`/`validate`/`evaluate` endpoints (`Motiv.Serialization.AspNetCore`, `app.MapMotivRules(basePath, registry, options)` with `options.AddModel<T>("id")`), the headless TS core (`@motiv/rules-core`), and the headless React adapter (`@motiv/rules-react`). This plan wires them into a runnable demo.

Read the design spec `docs/superpowers/specs/2026-07-16-rules-engine-frontend-ui-design.md` (§Frontend → Demo app) and the two UI package plans (`2026-07-18-rules-core-typescript-package.md`, `2026-07-18-rules-react-adapter.md`) for the exact APIs.

### ⚠️ Environment prerequisites
- **Node ≥20 + pnpm ≥9** (provisioned: Node 26, pnpm 9.15) — all `pnpm` commands run **from the `ui/` directory**.
- **.NET 10 with the ASP.NET Core 10 runtime** (installed) — `dotnet` commands run from the repo root.
- **Playwright (Task 7) needs a one-time Chromium download** via `pnpm --dir ui/apps/demo exec playwright install chromium`. If that download is blocked in your environment, the E2E test cannot execute here — everything else (host, demo build, component tests) is fully verifiable, and the E2E will run in CI / on a dev machine. Note it and move on; do not treat a blocked browser download as a plan failure.

### The APIs you consume
- **`@motiv/rules-core`:** `RuleEditorStore` (`.getState()`, `.subscribe()`, `.replaceNode(path,node)`, `.wrapInOperator(path,op,sibling)`, `.addOperand(operatorPath,node)`, `.removeOperand(elementPath)`, `.unwrap(path)`); `RulesApiClient({ baseUrl })` (`.getCatalog()`, `.validate(req)`, `.evaluate(req)`); `createValidationController(store, client, { modelType, debounceMs })` → dispose fn; guards `nodeKind`/`isSpecNode`/`isBinaryNode`/`isNotNode`; types `RuleDocument`, `RuleNode`, `CatalogEntry`, `EvaluateRequest`.
- **`@motiv/rules-react`:** `RuleEditorProvider`, `useRuleEditorStore`, `useRuleEditor`, `useRuleNode`, `useCatalog`, `useEvaluation`, `RuleTree` (render-prop `({ path, node, level, errors })`), `JustificationTree` (render-prop `({ row, toggle })`).
- **Backend:** `MapMotivRules("/api/rules", registry, options)`; `SpecRegistry().Register(name, spec, description)`; `MotivRulesOptions().AddModel<TModel>("id")`.

### Design decisions (do not deviate without escalating)
- **Same-origin, one process.** The host serves the API at `/api/rules` and the SPA at `/`. The demo's `RulesApiClient` uses `baseUrl: '/api/rules'` (relative). In dev, Vite proxies `/api` to the host; in prod/E2E the host serves the built SPA.
- **The demo is a proof, not a product.** Keep panes minimal but functional: enough to build a composite rule, see it validate, and evaluate it. No design-system polish; simple inline structure is fine (this is the one place a little styling is acceptable, but keep it trivial).
- **Panes are testable in isolation.** Each pane takes what it needs (`client` prop and/or the context store) so component tests can inject a mock client. `App` wires the real ones.
- **The demo build outputs into the host `wwwroot`** (git-ignored) so the host serves it. `wwwroot` is a build artifact.

### File structure
```
src/examples/Motiv.RulesEngine.Sample/
├── Motiv.RulesEngine.Sample.csproj      (Microsoft.NET.Sdk.Web, net10)
├── Program.cs                           (Customer specs + MapMotivRules + static/SPA)
└── wwwroot/.gitignore                   (ignore built SPA assets)
ui/apps/demo/
├── package.json
├── tsconfig.json
├── vite.config.ts                       (react plugin, /api proxy, outDir → host wwwroot, vitest)
├── playwright.config.ts
├── index.html
├── test/setup.ts
├── src/
│   ├── main.tsx
│   ├── App.tsx
│   ├── BuilderPane.tsx
│   ├── JsonPane.tsx
│   └── EvaluatePane.tsx
├── test/
│   ├── BuilderPane.test.tsx
│   ├── JsonPane.test.tsx
│   └── EvaluatePane.test.tsx
└── e2e/smoke.spec.ts
Motiv.slnx                               (add the sample host under /Examples/)
```

### Commands
- Host build: `dotnet build src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj`
- Host run (fixed port): `dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100`
- Demo (from `ui/`): `pnpm -C apps/demo test` / `pnpm -C apps/demo build` / `pnpm -C apps/demo typecheck`
- E2E (from `ui/`): `pnpm -C apps/demo e2e`

---

## Task 1: Sample ASP.NET host

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj`, `Program.cs`, `wwwroot/.gitignore`
- Modify: `Motiv.slnx`

- [ ] **Step 1: Create the project file**

`src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\Motiv.Serialization.AspNetCore\Motiv.Serialization.AspNetCore.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the host program**

`src/examples/Motiv.RulesEngine.Sample/Program.cs`:
```csharp
using Motiv;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;

var registry = new SpecRegistry()
    .Register(
        "is-active",
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active")
            .WhenFalse("customer is inactive")
            .Create(),
        "Whether the customer account is active")
    .Register(
        "is-adult",
        Spec.Build((Customer c) => c.Age >= 18)
            .WhenTrue("customer is an adult")
            .WhenFalse("customer is a minor")
            .Create(),
        "Whether the customer is 18 or older")
    .Register(
        "has-orders",
        Spec.Build((Customer c) => c.OrderCount > 0)
            .WhenTrue("customer has orders")
            .WhenFalse("customer has no orders")
            .Create(),
        "Whether the customer has placed at least one order");

var options = new MotivRulesOptions().AddModel<Customer>("customer");

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapMotivRules("/api/rules", registry, options);
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>The demo model that rules are evaluated against.</summary>
public sealed record Customer(int Age, bool IsActive, int OrderCount);
```

- [ ] **Step 3: Ignore the built SPA assets**

`src/examples/Motiv.RulesEngine.Sample/wwwroot/.gitignore`:
```gitignore
# Built SPA assets are produced by `pnpm -C apps/demo build`; do not commit them.
*
!.gitignore
```

- [ ] **Step 4: Register the project in `Motiv.slnx`**

In `Motiv.slnx`, add this line inside the `<Folder Name="/Examples/">` block, alongside the other example projects:
```xml
    <Project Path="src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj" />
```

- [ ] **Step 5: Build and smoke-test the host**

Run:
```bash
dotnet build src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj
```
Expected: build succeeds.

Then verify the catalog endpoint responds (start the host in the background, curl, stop it):
```bash
dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100 &
HOST_PID=$!
sleep 6
curl -s http://localhost:5100/api/rules/catalog
kill $HOST_PID
```
Expected: JSON array containing the three specs (`is-active`, `is-adult`, `has-orders`) with `"modelType":"customer"`.

- [ ] **Step 6: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample Motiv.slnx
git commit -m "feat(sample): ASP.NET host exposing MapMotivRules over a Customer domain"
```

---

## Task 2: Demo app scaffold

**Files:**
- Create: `ui/apps/demo/package.json`, `tsconfig.json`, `vite.config.ts`, `index.html`, `test/setup.ts`, `src/main.tsx`, `src/App.tsx` (placeholder), `test/App.test.tsx`

- [ ] **Step 1: Create the package + config files**

`ui/apps/demo/package.json`:
```json
{
  "name": "@motiv/rules-demo",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "typecheck": "tsc --noEmit",
    "test": "vitest run",
    "e2e": "vite build && playwright test"
  },
  "dependencies": {
    "@motiv/rules-core": "workspace:*",
    "@motiv/rules-react": "workspace:*",
    "react": "^18.3.1",
    "react-dom": "^18.3.1"
  },
  "devDependencies": {
    "@playwright/test": "^1.49.1",
    "@testing-library/dom": "^10.4.0",
    "@testing-library/react": "^16.1.0",
    "@testing-library/user-event": "^14.5.2",
    "@types/node": "^26.1.1",
    "@types/react": "^18.3.12",
    "@types/react-dom": "^18.3.1",
    "@vitejs/plugin-react": "^4.3.4",
    "jsdom": "^25.0.1",
    "typescript": "^5.7.2",
    "vite": "^6.0.5",
    "vitest": "^2.1.8"
  }
}
```

`ui/apps/demo/tsconfig.json`:
```json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "rootDir": ".",
    "noEmit": true,
    "jsx": "react-jsx",
    "types": ["node"],
    "lib": ["ES2022", "DOM", "DOM.Iterable"]
  },
  "include": ["src", "test", "e2e", "vite.config.ts", "playwright.config.ts"]
}
```

`ui/apps/demo/vite.config.ts`:
```ts
/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5100',
    },
  },
  build: {
    outDir: '../../../src/examples/Motiv.RulesEngine.Sample/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./test/setup.ts'],
  },
});
```

`ui/apps/demo/index.html`:
```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Motiv Rules Demo</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`ui/apps/demo/test/setup.ts`:
```ts
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

afterEach(() => cleanup());
```

- [ ] **Step 2: Create the entry point and placeholder App**

`ui/apps/demo/src/main.tsx`:
```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App.js';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

`ui/apps/demo/src/App.tsx`:
```tsx
export function App() {
  return <h1>Motiv Rules Demo</h1>;
}
```

`ui/apps/demo/test/App.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { App } from '../src/App.js';

describe('App', () => {
  it('renders the demo heading', () => {
    render(<App />);
    expect(screen.getByRole('heading', { name: 'Motiv Rules Demo' })).toBeDefined();
  });
});
```

- [ ] **Step 3: Install, test, and build**

Run (from `ui/`):
```bash
pnpm install
pnpm -C apps/demo test
pnpm -C apps/demo typecheck
pnpm -C apps/demo build
```
Expected: install links the two workspace packages; the App test passes; typecheck clean; `vite build` writes assets into `src/examples/Motiv.RulesEngine.Sample/wwwroot` (an `index.html` appears there).

- [ ] **Step 4: Commit**

```bash
git add ui/apps/demo ui/pnpm-lock.yaml
git commit -m "feat(demo): scaffold Vite React demo app"
```

---

## Task 3: App shell (store, client, validation, three-pane layout)

**Files:**
- Modify: `ui/apps/demo/src/App.tsx`
- Create: `ui/apps/demo/src/BuilderPane.tsx`, `ui/apps/demo/src/JsonPane.tsx`, `ui/apps/demo/src/EvaluatePane.tsx` (placeholders)
- Test: `ui/apps/demo/test/App.test.tsx` (replace)

- [ ] **Step 1: Write placeholder panes so App composes**

`ui/apps/demo/src/BuilderPane.tsx`:
```tsx
import type { RulesApiClient } from '@motiv/rules-core';

export function BuilderPane(_props: { client: RulesApiClient }) {
  return <section aria-label="Builder" />;
}
```

`ui/apps/demo/src/JsonPane.tsx`:
```tsx
export function JsonPane() {
  return <section aria-label="Document" />;
}
```

`ui/apps/demo/src/EvaluatePane.tsx`:
```tsx
import type { RulesApiClient } from '@motiv/rules-core';

export function EvaluatePane(_props: { client: RulesApiClient }) {
  return <section aria-label="Evaluate" />;
}
```

- [ ] **Step 2: Write the failing App test**

Replace `ui/apps/demo/test/App.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RuleEditorStore, RulesApiClient } from '@motiv/rules-core';
import { App } from '../src/App.js';

function testClient(): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue([]),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
  } as unknown as RulesApiClient;
}

describe('App', () => {
  it('renders the three panes', () => {
    render(<App client={testClient()} store={new RuleEditorStore({ rule: { spec: 'is-active' } })} />);
    expect(screen.getByRole('region', { name: 'Builder' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Document' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeDefined();
  });
});
```

- [ ] **Step 3: Run the test to verify it fails**

Run:
```bash
pnpm -C apps/demo test test/App.test.tsx
```
Expected: FAILS — `App` does not accept props / does not render regions yet.

- [ ] **Step 4: Write the App shell**

Replace `ui/apps/demo/src/App.tsx`:
```tsx
import { useEffect, useMemo } from 'react';
import { RuleEditorStore, RulesApiClient, createValidationController } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from './BuilderPane.js';
import { JsonPane } from './JsonPane.js';
import { EvaluatePane } from './EvaluatePane.js';

const MODEL_TYPE = 'customer';

/** The demo shell: owns the store + client, runs debounced validation, and lays out the three panes. */
export function App(props: { client?: RulesApiClient; store?: RuleEditorStore }) {
  const store = useMemo(
    () => props.store ?? new RuleEditorStore({ rule: { spec: 'is-active' } }),
    [props.store],
  );
  const client = useMemo(
    () => props.client ?? new RulesApiClient({ baseUrl: '/api/rules' }),
    [props.client],
  );

  useEffect(
    () => createValidationController(store, client, { modelType: MODEL_TYPE, debounceMs: 300 }),
    [store, client],
  );

  return (
    <RuleEditorProvider store={store}>
      <main style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '1rem', padding: '1rem' }}>
        <BuilderPane client={client} />
        <JsonPane />
        <EvaluatePane client={client} />
      </main>
    </RuleEditorProvider>
  );
}

export { MODEL_TYPE };
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
pnpm -C apps/demo test test/App.test.tsx
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/src/App.tsx ui/apps/demo/src/BuilderPane.tsx ui/apps/demo/src/JsonPane.tsx ui/apps/demo/src/EvaluatePane.tsx ui/apps/demo/test/App.test.tsx
git commit -m "feat(demo): app shell with store, client, validation, and three-pane layout"
```

---

## Task 4: BuilderPane

**Files:**
- Modify: `ui/apps/demo/src/BuilderPane.tsx`
- Test: `ui/apps/demo/test/BuilderPane.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/apps/demo/test/BuilderPane.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RuleEditorStore, type CatalogEntry, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../src/BuilderPane.js';

const catalog: CatalogEntry[] = [
  { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'active' },
  { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'adult' },
];

function client(): RulesApiClient {
  return { getCatalog: vi.fn().mockResolvedValue(catalog) } as unknown as RulesApiClient;
}

function renderWith(store: RuleEditorStore) {
  return render(
    <RuleEditorProvider store={store}>
      <BuilderPane client={client()} />
    </RuleEditorProvider>,
  );
}

describe('BuilderPane', () => {
  it('changes a leaf spec via the catalog select', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);

    const select = await screen.findByLabelText('spec at $.rule');
    fireEvent.change(select, { target: { value: 'is-adult' } });

    expect(store.getState().document.rule).toEqual({ spec: 'is-adult' });
  });

  it('wraps the root leaf in an AND operator', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);

    await screen.findByLabelText('spec at $.rule');
    fireEvent.click(screen.getByRole('button', { name: 'AND at $.rule' }));

    await waitFor(() => {
      const rule = store.getState().document.rule as { and?: unknown[] };
      expect(rule.and).toHaveLength(2);
    });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C apps/demo test test/BuilderPane.test.tsx
```
Expected: FAILS — the placeholder pane renders nothing.

- [ ] **Step 3: Write the pane**

Replace `ui/apps/demo/src/BuilderPane.tsx`:
```tsx
import {
  isBinaryNode, isSpecNode, nodeKind,
  type CatalogEntry, type RulesApiClient,
} from '@motiv/rules-core';
import { RuleTree, useCatalog, useRuleEditorStore } from '@motiv/rules-react';

function firstSpecName(catalog: CatalogEntry[]): string {
  return catalog[0]?.name ?? 'spec';
}

/** Minimal catalog-driven rule builder: pick specs, wrap in AND/OR, add/remove operands. */
export function BuilderPane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const catalogState = useCatalog(props.client);
  const catalog = catalogState.status === 'ready' ? catalogState.data : [];

  return (
    <section aria-label="Builder">
      <h2>Builder</h2>
      {catalogState.status === 'loading' && <p>Loading catalog…</p>}
      <RuleTree>
        {({ path, node, level, errors }) => (
          <div style={{ paddingLeft: (level - 1) * 16 }}>
            {isSpecNode(node) ? (
              <>
                <label>
                  <span hidden>spec at {path}</span>
                  <select
                    aria-label={`spec at ${path}`}
                    value={node.spec}
                    onChange={(e) => store.replaceNode(path, { spec: e.target.value })}
                  >
                    {catalog.map((entry) => (
                      <option key={entry.name} value={entry.name}>{entry.name}</option>
                    ))}
                  </select>
                </label>
                <button
                  type="button"
                  aria-label={`AND at ${path}`}
                  onClick={() => store.wrapInOperator(path, 'and', { spec: firstSpecName(catalog) })}
                >AND</button>
                <button
                  type="button"
                  aria-label={`OR at ${path}`}
                  onClick={() => store.wrapInOperator(path, 'or', { spec: firstSpecName(catalog) })}
                >OR</button>
              </>
            ) : (
              <span>{nodeKind(node)}</span>
            )}
            {isBinaryNode(node) && (
              <button
                type="button"
                aria-label={`add operand at ${path}`}
                onClick={() => store.addOperand(path, { spec: firstSpecName(catalog) })}
              >+ operand</button>
            )}
            {path.endsWith(']') && (
              <button type="button" aria-label={`remove ${path}`} onClick={() => store.removeOperand(path)}>×</button>
            )}
            {errors.length > 0 && <span role="alert"> {errors.map((e) => e.message).join('; ')}</span>}
          </div>
        )}
      </RuleTree>
    </section>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C apps/demo test test/BuilderPane.test.tsx
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/BuilderPane.tsx ui/apps/demo/test/BuilderPane.test.tsx
git commit -m "feat(demo): catalog-driven rule builder pane"
```

---

## Task 5: JsonPane

**Files:**
- Modify: `ui/apps/demo/src/JsonPane.tsx`
- Test: `ui/apps/demo/test/JsonPane.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/apps/demo/test/JsonPane.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { JsonPane } from '../src/JsonPane.js';

function renderWith(store: RuleEditorStore) {
  return render(
    <RuleEditorProvider store={store}>
      <JsonPane />
    </RuleEditorProvider>,
  );
}

describe('JsonPane', () => {
  it('shows the live document JSON', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    expect(screen.getByLabelText('rule document').textContent).toContain('"spec": "is-active"');

    act(() => store.replaceNode('$.rule', { spec: 'is-adult' }));
    expect(screen.getByLabelText('rule document').textContent).toContain('"spec": "is-adult"');
  });

  it('lists validation errors set on the store', () => {
    const store = new RuleEditorStore({ rule: { spec: 'nope' } });
    renderWith(store);
    act(() => store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'unknown spec' }]));
    expect(screen.getByText(/UnknownSpec/)).toBeDefined();
    expect(screen.getByText(/unknown spec/)).toBeDefined();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C apps/demo test test/JsonPane.test.tsx
```
Expected: FAILS — the placeholder pane renders nothing useful.

- [ ] **Step 3: Write the pane**

Replace `ui/apps/demo/src/JsonPane.tsx`:
```tsx
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';

/** Shows the live rule document as formatted JSON and lists current validation errors. */
export function JsonPane() {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    <section aria-label="Document">
      <h2>Document</h2>
      <pre aria-label="rule document">{JSON.stringify(state.document, null, 2)}</pre>
      {state.errors.length > 0 && (
        <ul aria-label="validation errors">
          {state.errors.map((error, i) => (
            <li key={`${error.path}-${i}`} role="alert">{error.code} at {error.path}: {error.message}</li>
          ))}
        </ul>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C apps/demo test test/JsonPane.test.tsx
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/JsonPane.tsx ui/apps/demo/test/JsonPane.test.tsx
git commit -m "feat(demo): live JSON + validation errors pane"
```

---

## Task 6: EvaluatePane

**Files:**
- Modify: `ui/apps/demo/src/EvaluatePane.tsx`
- Test: `ui/apps/demo/test/EvaluatePane.test.tsx`

- [ ] **Step 1: Write the failing test**

`ui/apps/demo/test/EvaluatePane.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RuleEditorStore, type EvaluationResult, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { EvaluatePane } from '../src/EvaluatePane.js';

const evaluation: EvaluationResult = {
  satisfied: true,
  reason: 'customer is active',
  assertions: ['customer is active'],
  values: ['customer is active'],
  justification: 'customer is active',
  explanation: { assertions: ['customer is active'], underlying: [] },
};

function client(evaluate = vi.fn().mockResolvedValue(evaluation)): RulesApiClient {
  return { evaluate } as unknown as RulesApiClient;
}

describe('EvaluatePane', () => {
  it('evaluates the current document against the sample model and renders the outcome', async () => {
    const evaluate = vi.fn().mockResolvedValue(evaluation);
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    render(
      <RuleEditorProvider store={store}>
        <EvaluatePane client={client(evaluate)} />
      </RuleEditorProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Evaluate' }));

    await waitFor(() => expect(screen.getByLabelText('outcome').textContent).toContain('Satisfied'));
    expect(evaluate).toHaveBeenCalledWith(
      expect.objectContaining({ modelType: 'customer', document: store.getState().document }),
    );
    expect(screen.getByText('customer is active')).toBeDefined();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
pnpm -C apps/demo test test/EvaluatePane.test.tsx
```
Expected: FAILS — the placeholder pane renders nothing.

- [ ] **Step 3: Write the pane**

Replace `ui/apps/demo/src/EvaluatePane.tsx`:
```tsx
import { useState } from 'react';
import type { RulesApiClient } from '@motiv/rules-core';
import { JustificationTree, useEvaluation, useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import { MODEL_TYPE } from './App.js';

const SAMPLE_MODEL = '{\n  "age": 30,\n  "isActive": true,\n  "orderCount": 2\n}';

/** Evaluates the current document against a sample model and renders the explanation tree. */
export function EvaluatePane(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const evaluation = useEvaluation(props.client);
  const [modelText, setModelText] = useState(SAMPLE_MODEL);
  const [parseError, setParseError] = useState<string | null>(null);

  const run = (): void => {
    let model: unknown;
    try {
      model = JSON.parse(modelText);
    } catch {
      setParseError('Sample model is not valid JSON.');
      return;
    }
    setParseError(null);
    void evaluation.evaluate({ modelType: MODEL_TYPE, document: state.document, model });
  };

  return (
    <section aria-label="Evaluate">
      <h2>Evaluate</h2>
      <label>
        <span>Sample model</span>
        <textarea aria-label="sample model" value={modelText} onChange={(e) => setModelText(e.target.value)} rows={5} />
      </label>
      <button type="button" onClick={run}>Evaluate</button>
      {parseError && <p role="alert">{parseError}</p>}
      {evaluation.status === 'error' && <p role="alert">Evaluation failed.</p>}
      {evaluation.status === 'ready' && (
        <>
          <p aria-label="outcome">{evaluation.result.satisfied ? 'Satisfied' : 'Not satisfied'}</p>
          <JustificationTree explanation={evaluation.result.explanation}>
            {({ row, toggle }) => (
              <div style={{ paddingLeft: row.depth * 16 }}>
                {row.hasChildren && (
                  <button type="button" onClick={() => toggle(row.id)}>{row.collapsed ? '▸' : '▾'}</button>
                )}
                <span>{row.assertions.join(', ')}</span>
              </div>
            )}
          </JustificationTree>
        </>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
pnpm -C apps/demo test test/EvaluatePane.test.tsx
```
Expected: PASS.

- [ ] **Step 5: Run the whole demo suite + typecheck + build**

Run:
```bash
pnpm -C apps/demo test
pnpm -C apps/demo typecheck
pnpm -C apps/demo build
```
Expected: all suites (App, BuilderPane, JsonPane, EvaluatePane) pass; typecheck clean; build writes the SPA into the host `wwwroot`.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/src/EvaluatePane.tsx ui/apps/demo/test/EvaluatePane.test.tsx
git commit -m "feat(demo): evaluate pane with justification tree"
```

---

## Task 7: Playwright end-to-end smoke test

**Files:**
- Create: `ui/apps/demo/playwright.config.ts`, `ui/apps/demo/e2e/smoke.spec.ts`

- [ ] **Step 1: Write the Playwright config**

`ui/apps/demo/playwright.config.ts`:
```ts
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  use: {
    baseURL: 'http://localhost:5100',
  },
  webServer: {
    command: 'dotnet run --project ../../../src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100',
    url: 'http://localhost:5100/api/rules/catalog',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
```

The `e2e` package script (`vite build && playwright test`) builds the SPA into `wwwroot` first, then Playwright starts the host (which serves it) and waits for the catalog endpoint before running the spec.

- [ ] **Step 2: Write the smoke spec**

`ui/apps/demo/e2e/smoke.spec.ts`:
```ts
import { test, expect } from '@playwright/test';

test('build a rule, then evaluate it end to end', async ({ page }) => {
  await page.goto('/');

  // Builder loaded from the live catalog: the root leaf select is present.
  const rootSelect = page.getByLabel('spec at $.rule');
  await expect(rootSelect).toBeVisible();

  // Build a composite: wrap the root in AND (adds a second operand).
  await page.getByRole('button', { name: 'AND at $.rule' }).click();

  // The JSON pane reflects the composite document.
  await expect(page.getByLabel('rule document')).toContainText('"and"');

  // Evaluate against the prefilled sample model.
  await page.getByRole('button', { name: 'Evaluate' }).click();

  // An outcome is rendered (Satisfied / Not satisfied).
  await expect(page.getByLabel('outcome')).toContainText(/Satisfied|Not satisfied/);
});
```

- [ ] **Step 3: Install the browser and run the E2E**

Run (from `ui/`):
```bash
pnpm -C apps/demo exec playwright install chromium
pnpm -C apps/demo e2e
```
Expected: the browser downloads, the SPA builds, the host starts, and the smoke test passes.

> If `playwright install chromium` cannot download the browser in this environment, record that the E2E could not be executed here (it will run in CI / on a dev machine) and proceed — do not block the plan on it. The config and spec are still committed.

- [ ] **Step 4: Commit**

```bash
git add ui/apps/demo/playwright.config.ts ui/apps/demo/e2e/smoke.spec.ts
git commit -m "test(demo): Playwright end-to-end smoke test"
```

---

## Task 8: Demo README + mandatory simplification review

**Files:**
- Create: `ui/apps/demo/README.md`
- Review: `ui/apps/demo/src/`, `src/examples/Motiv.RulesEngine.Sample/Program.cs`

- [ ] **Step 1: Write a short README documenting how to run the demo**

`ui/apps/demo/README.md`:
```markdown
# Motiv Rules Demo

A three-pane demo of the Motiv rules-engine frontend: build a rule from the spec
catalog, watch it validate live, and evaluate it against a sample `Customer`.

## Run it

From the repo root, in two terminals:

```bash
# 1. The API + host (also serves the built SPA at http://localhost:5100)
dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100

# 2. The demo with hot reload (proxies /api to the host)
pnpm -C ui/apps/demo dev
```

Open the Vite URL for development, or build the SPA into the host and use one origin:

```bash
pnpm -C ui/apps/demo build   # outputs into the host's wwwroot
# then browse http://localhost:5100
```

## Test

```bash
pnpm -C ui/apps/demo test    # component tests (jsdom)
pnpm -C ui/apps/demo e2e     # Playwright smoke (builds SPA, starts host)
```
```

- [ ] **Step 2: Commit the README**

```bash
git add ui/apps/demo/README.md
git commit -m "docs(demo): how to run and test the demo"
```

- [ ] **Step 3: Spawn the code-simplifier agent**

Per CLAUDE.md, dispatch `code-simplifier:code-simplifier` over the demo `src/` files and the host `Program.cs`. Instruct it to preserve the public behavior, the pane props (for testability), the same-origin `/api/rules` base URL, and the minimal-but-functional scope — and to focus on duplication, convoluted logic, and naming (not to add product polish).

- [ ] **Step 4: Apply accepted suggestions and re-run**

If it proposes changes, apply them, then run:
```bash
pnpm -C apps/demo test && pnpm -C apps/demo typecheck
dotnet build src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj
```
Expected: demo suites pass, typecheck clean, host builds.

- [ ] **Step 5: Commit any changes**

```bash
git add ui/apps/demo/src src/examples/Motiv.RulesEngine.Sample
git commit -m "refactor(demo): simplify per review"
```

(Skip if nothing changed.)

---

## Self-Review Notes (author)

- **Spec coverage (design §Frontend → Demo app):**
  - *Three panes — styled business-user builder, live JSON + validation errors, evaluate → rendered explanation tree* → Tasks 4 (BuilderPane), 5 (JsonPane), 6 (EvaluatePane).
  - *Served by a small ASP.NET host using `MapMotivRules` with a handful of registered specs* → Task 1 (`Motiv.RulesEngine.Sample`, three `Customer` specs).
  - *Each pane exercises one persona (business user / developer / result viewer)* → builder, JSON+errors, evaluate+explanation respectively.
  - *One Playwright smoke test drives the demo end-to-end (build rule → validate → evaluate)* → Task 7.
  - *CI Node job* → already added in Plan 3 (`ui.yml`, `pnpm -r`); it now also runs the demo's `typecheck`/`build`/`test` automatically. The Playwright `e2e` script is deliberately excluded from the default `test` script (needs a browser + host), so `pnpm -r test` stays fast and hermetic.
- **Deferred / out of scope:** rich builder UX (drag-and-drop, per-node decoration editing), multi-model selection, authentication, and persistence — the demo is a proof, kept minimal. A CI e2e job is not added (browser download + dotnet host startup make it heavy); the E2E runs on demand via `pnpm -C ui/apps/demo e2e`.
- **Placeholder scan:** none — every step has concrete code/commands.
- **Type/name consistency:** `App` (props `{ client?, store? }`, exports `MODEL_TYPE`), `BuilderPane`/`JsonPane`/`EvaluatePane` (pane props), the `/api/rules` base URL, model id `customer`, and the `Customer(Age, IsActive, OrderCount)` record are used identically across host, panes, and tests. All imports (`RuleEditorStore`, `RulesApiClient`, `createValidationController`, guards, `RuleEditorProvider`, `RuleTree`, `JustificationTree`, `useCatalog`/`useEvaluation`/`useRuleEditor`/`useRuleEditorStore`) match the committed public surfaces of `@motiv/rules-core` and `@motiv/rules-react`. The evaluate request shape (`{ modelType, document, model }`) and the sample-model camelCase keys (`age`/`isActive`/`orderCount`) match the backend's `EvaluateRequest` and STJ web-default binding for the `Customer` record.
- **Environment risk:** the Playwright browser download is the one step that may not run in a restricted sandbox; called out in Task 7 and treated as non-blocking.
```
