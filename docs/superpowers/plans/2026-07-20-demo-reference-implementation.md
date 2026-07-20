# Demo Reference-Implementation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the bare rules demo into a runnable, styled, forkable reference implementation whose accordion builder covers the boolean grammar plus higher-order quantifiers, with one-command run + Docker.

**Architecture:** Six phases. **A** exposes registered collections by folding `GET /catalog` into `{ specs, collections }` (+ `SpecRegistry.Collections`). **B** enriches the sample host with an `Order` collection so higher-order is evaluable. **C** lays a vanilla-CSS token system and removes inline styles. **D** rebuilds the builder as a single-open accordion (boolean grammar + a `QuantifierNode` for higher-order + disabled expression/parameter extension points). **E** adds run ergonomics (script, Makefile, Docker). **F** adds tests + E2E + final verification. Each phase builds on a green predecessor.

**Tech Stack:** .NET 10 (`Motiv.Serialization`, `Motiv.Serialization.AspNetCore`); TypeScript `@motiv/rules-core` (tsup) + `@motiv/rules-react`; Vite React demo; Vitest + Testing Library; Playwright; xUnit + Shouldly.

**Design spec:** `docs/superpowers/specs/2026-07-19-rules-engine-demo-reference-implementation-design.md`

**Conventions:**
- C# tests: xUnit + Shouldly, AAA comments, raw-string JSON, namespace per project. Run one: `dotnet test <proj> --filter "FullyQualifiedName~<Class>.<Method>" -f net10.0` (only net10.0 runs in this sandbox; other TFMs fail to launch — ignore). Multi-TFM **build** must stay clean: `dotnet build`.
- TS tests: Vitest + Testing Library, `describe/it`, `screen.findByLabelText`, `fireEvent`. Run: `pnpm -C <pkgOrApp> test`. Build/typecheck: `pnpm -C <pkgOrApp> build`.
- The demo store API (`@motiv/rules-core`): `replaceNode(path,node)`, `wrapInOperator(path,op,sibling)`, `addOperand(opPath,node)`, `removeOperand(elemPath)`, `unwrap(path)`, `setDecoration(path,{whenTrue?,whenFalse?})`, `setName(path,name?)`. Node guards: `isSpecNode`, `isBinaryNode`, `isNotNode`, `isHigherOrderNode`, `nodeKind`, `binaryOperator`, `operandsOf`, `higherOrderKey`. Path helpers: `getNode`, `listPaths`.

---

## Phase A — Collection discovery (fold `/catalog`)

### Task A1: `SpecRegistry.Collections` enumeration

**Files:**
- Create: `src/Motiv.Serialization/CollectionRegistryEntry.cs`
- Modify: `src/Motiv.Serialization/SpecRegistry.cs`, `src/Motiv.Serialization/CollectionBinding.cs`
- Test: `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`

- [ ] **Step 1: Write the failing test** (append to `SpecRegistryTests`)

```csharp
[Fact]
public void Should_enumerate_registered_collections_with_parent_and_element_types()
{
    var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);

    var collections = registry.Collections;

    var entry = collections.ShouldHaveSingleItem();
    entry.ParentType.ShouldBe(typeof(Cart));
    entry.Path.ShouldBe("orders");
    entry.ElementType.ShouldBe(typeof(Order));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~SpecRegistryTests.Should_enumerate_registered_collections_with_parent_and_element_types" -f net10.0`
Expected: FAIL to compile (`Collections`, `ParentType` don't exist).

- [ ] **Step 3: Add `CollectionRegistryEntry`**

Create `src/Motiv.Serialization/CollectionRegistryEntry.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>Describes a collection projection registered on a <see cref="SpecRegistry"/>.</summary>
/// <param name="ParentType">The model type the collection is selected from.</param>
/// <param name="Path">The path higher-order rule nodes reference the collection by.</param>
/// <param name="ElementType">The element type of the collection.</param>
public sealed record CollectionRegistryEntry(Type ParentType, string Path, Type ElementType);
```

- [ ] **Step 4: Expose parent type + path on the binding, and enumerate**

In `src/Motiv.Serialization/CollectionBinding.cs`, add abstract members carrying the key so the registry can project entries without reflection. Change the abstract base and concrete:

```csharp
internal abstract class CollectionBinding<TParent>
{
    public abstract Type ElementType { get; }
    public abstract SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors);
}
```
stays as-is; add a **non-generic** marker so the registry (which stores `object`) can read parent/element/path uniformly. Add to the same file:

```csharp
/// <summary>Non-generic view of a registered collection, for enumeration.</summary>
internal interface ICollectionBindingInfo
{
    Type ParentType { get; }
    Type ElementType { get; }
}
```
and make the concrete implement it:

```csharp
internal sealed class CollectionBinding<TParent, TElement>(Func<TParent, IEnumerable<TElement>> selector)
    : CollectionBinding<TParent>, ICollectionBindingInfo
{
    public override Type ElementType => typeof(TElement);
    Type ICollectionBindingInfo.ParentType => typeof(TParent);
    Type ICollectionBindingInfo.ElementType => typeof(TElement);
    // BindHigherOrder unchanged
    public override SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors)
    {
        var child = RuleBinder.BindElement<TElement>(node.Children[0], registry, errors);
        if (child is null) return null;
        var higherOrder = HigherOrder.Build(child, node.Operator, node.N);
        return higherOrder.ChangeModelTo<TParent>(selector);
    }
}
```

In `src/Motiv.Serialization/SpecRegistry.cs`, the `_collections` dictionary is keyed by `(Type Parent, string Path)`. Add:

```csharp
/// <summary>All registered collection projections. Intended for read-only enumeration after population.</summary>
public IReadOnlyCollection<CollectionRegistryEntry> Collections =>
    _collections
        .Select(kv => new CollectionRegistryEntry(kv.Key.Parent, kv.Key.Path, ((ICollectionBindingInfo)kv.Value).ElementType))
        .ToArray();
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~SpecRegistryTests" -f net10.0`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization/CollectionRegistryEntry.cs src/Motiv.Serialization/CollectionBinding.cs \
        src/Motiv.Serialization/SpecRegistry.cs src/Motiv.Serialization.Tests/SpecRegistryTests.cs
git commit -m "feat(rules-serialization): enumerate registered collections"
```

---

### Task A2: fold `GET /catalog` into `{ specs, collections }`

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs`, `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`
- Modify: `schemas/rules-api.v1.yaml`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs`

- [ ] **Step 1: Rewrite the failing test** — replace the body of `Should_list_registered_specs_with_model_id_and_description` and add a collections test:

```csharp
[Fact]
public async Task Should_list_specs_and_collections()
{
    // Arrange
    var registry = new SpecRegistry()
        .Register("is-positive", IsPositive, "Whether the number is positive")
        .RegisterCollection<Basket, int>("items", b => b.Items);
    var options = new MotivRulesOptions().AddModel<int>("number").AddModel<Basket>("basket");
    await using var app = await TestApp.StartAsync(registry, options);
    var client = app.GetTestClient();

    // Act
    var response = await client.GetAsync("/api/rules/catalog");

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();

    var spec = body.GetProperty("specs")[0];
    spec.GetProperty("name").GetString()!.ShouldBe("is-positive");
    spec.GetProperty("modelType").GetString()!.ShouldBe("number");
    spec.GetProperty("description").GetString()!.ShouldBe("Whether the number is positive");

    var collection = body.GetProperty("collections")[0];
    collection.GetProperty("path").GetString()!.ShouldBe("items");
    collection.GetProperty("parentModelType").GetString()!.ShouldBe("basket");
    collection.GetProperty("elementModelType").GetString()!.ShouldBe("number");
}

private sealed class Basket
{
    public IReadOnlyList<int> Items { get; }
    public Basket(IReadOnlyList<int> items) => Items = items;
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/Motiv.Serialization.AspNetCore.Tests --filter "FullyQualifiedName~CatalogEndpointTests" -f net10.0`
Expected: FAIL — `/catalog` still returns a flat array (`body.GetProperty("specs")` throws).

- [ ] **Step 3: Add the contract records** — in `RulesContracts.cs`:

```csharp
/// <summary>A catalog listing for one registered collection projection.</summary>
/// <param name="Path">The path higher-order nodes reference the collection by.</param>
/// <param name="ParentModelType">The registered model-type id the collection is selected from.</param>
/// <param name="ElementModelType">The registered model-type id of the collection's elements.</param>
public sealed record CatalogCollection(string Path, string ParentModelType, string ElementModelType);

/// <summary>The full catalog: registered specs and collections.</summary>
public sealed record CatalogResponse(IReadOnlyList<CatalogEntry> Specs, IReadOnlyList<CatalogCollection> Collections);
```

- [ ] **Step 4: Build and return the folded catalog** — in `MotivRulesEndpoints.cs`, replace the `entries` array + `MapGet("/catalog", …)` with:

```csharp
var specs = registry.Entries
    .Select(entry => new CatalogEntry(
        entry.Name,
        options.ResolveModelId(entry.ModelType),
        entry.MetadataType.Name,
        entry.IsAsync,
        entry.Description))
    .ToArray();

var collections = registry.Collections
    .Select(c => new CatalogCollection(
        c.Path,
        options.ResolveModelId(c.ParentType),
        options.ResolveModelId(c.ElementType)))
    .ToArray();

var catalog = new CatalogResponse(specs, collections);

group.MapGet("/catalog", () => Results.Json(catalog, json));
```

- [ ] **Step 5: Update the OpenAPI doc** — in `schemas/rules-api.v1.yaml`, change the `/catalog` `200` response schema from an array of `CatalogEntry` to an object with `specs` (array of the existing entry schema) and `collections` (array of `{ path, parentModelType, elementModelType }` strings). Match the existing style of the file; add a `CatalogCollection` component schema and a `CatalogResponse` wrapper.

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test src/Motiv.Serialization.AspNetCore.Tests --filter "FullyQualifiedName~CatalogEndpointTests" -f net10.0`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/RulesContracts.cs src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs \
        schemas/rules-api.v1.yaml src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs
git commit -m "feat(rules-aspnetcore): fold catalog into { specs, collections }"
```

---

### Task A3: `@motiv/rules-core` — `Catalog` type + `getCatalog`

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts`, `ui/packages/rules-core/src/client.ts`
- Test: `ui/packages/rules-core/test/client.test.ts`

- [ ] **Step 1: Update the failing test** — in `client.test.ts`, find the catalog test and change its stubbed response + assertion to the folded shape. The test should stub a fetch returning `{ specs: [...], collections: [...] }` and assert `getCatalog()` resolves to that object. Concretely, replace the catalog test with:

```ts
it('gets the folded catalog of specs and collections', async () => {
  const catalog = {
    specs: [{ name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null }],
    collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
  };
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(catalog), { status: 200 }));
  const client = new RulesApiClient({ baseUrl: '/api/rules', fetch });

  const result = await client.getCatalog();

  expect(result).toEqual(catalog);
  expect(fetch).toHaveBeenCalledWith('/api/rules/catalog', { method: 'GET' });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `pnpm -C ui/packages/rules-core test`
Expected: FAIL (type + runtime: `getCatalog` returns `CatalogEntry[]`).

- [ ] **Step 3: Add types** — in `contracts.ts`, after `CatalogEntry`:

```ts
/** One catalog listing for a registered collection projection. */
export interface CatalogCollection {
  path: string;
  parentModelType: string;
  elementModelType: string;
}

/** The full catalog: registered specs and collections. */
export interface Catalog {
  specs: CatalogEntry[];
  collections: CatalogCollection[];
}
```

- [ ] **Step 4: Update the client** — in `client.ts`, change the import to include `Catalog`, and:

```ts
/** GET {baseUrl}/catalog */
async getCatalog(): Promise<Catalog> {
  const response = await this.#fetch(`${this.#baseUrl}/catalog`, { method: 'GET' });
  return this.#read<Catalog>(response);
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `pnpm -C ui/packages/rules-core test && pnpm -C ui/packages/rules-core build`
Expected: PASS + build clean.

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-core/src/contracts.ts ui/packages/rules-core/src/client.ts ui/packages/rules-core/test/client.test.ts
git commit -m "feat(rules-core): getCatalog returns { specs, collections }"
```

---

### Task A4: `@motiv/rules-react` — `useCatalog` returns `Catalog`

**Files:**
- Modify: `ui/packages/rules-react/src/useCatalog.ts`
- Test: `ui/packages/rules-react/test/useCatalog.test.tsx`

- [ ] **Step 1: Update the failing test** — in `useCatalog.test.tsx`, change the stubbed client to return a `Catalog` object and assert `state.data` is that object (with `.specs`/`.collections`). Keep the existing loading/ready/error structure; only the `data` shape changes.

- [ ] **Step 2: Run to verify it fails**

Run: `pnpm -C ui/packages/rules-react test`
Expected: FAIL (type: `data: CatalogEntry[]`).

- [ ] **Step 3: Update the hook** — in `useCatalog.ts`:

```ts
import type { Catalog, RulesApiClient } from '@motiv/rules-core';

export type CatalogState =
  | { status: 'loading' }
  | { status: 'ready'; data: Catalog }
  | { status: 'error'; error: unknown };
```
The body is unchanged (`client.getCatalog()` now resolves to `Catalog`).

- [ ] **Step 4: Run to verify it passes**

Run: `pnpm -C ui/packages/rules-react test && pnpm -C ui/packages/rules-react build`
Expected: PASS + build clean. (The demo app will not compile until Phase D updates `BuilderPane`; that is expected and handled there.)

- [ ] **Step 5: Commit**

```bash
git add ui/packages/rules-react/src/useCatalog.ts ui/packages/rules-react/test/useCatalog.test.tsx
git commit -m "feat(rules-react): useCatalog exposes { specs, collections }"
```

---

## Phase B — Sample host enrichment

### Task B1: add an `Order` collection to the sample host

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`

- [ ] **Step 1: Enrich the domain + registry** — edit `Program.cs`:
  - Add records:
    ```csharp
    public sealed record Customer(int Age, bool IsActive, int OrderCount, IReadOnlyList<Order>? Orders = null);
    public sealed record Order(decimal Total);
    ```
    (Keep `Orders` nullable-defaulted so existing `{ age, isActive, orderCount }` sample models still deserialize.)
  - Register an order-level spec and the collection (the selector coalesces null → empty):
    ```csharp
    .Register(
        "is-large-order",
        Spec.Build((Order o) => o.Total >= 100m)
            .WhenTrue("order is large")
            .WhenFalse("order is small")
            .Create(),
        "Whether an individual order total is 100 or more")
    // ... after building `registry` via the existing chain:
    ;
    registry.RegisterCollection<Customer, Order>("orders", c => c.Orders ?? []);
    ```
  Place `is-large-order` in the existing `.Register(...)` chain and add the `RegisterCollection` call on the built `registry` before `MapMotivRules`.

- [ ] **Step 2: Verify end-to-end by hand**

Run the host and post a higher-order document:
```bash
dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100 &
sleep 8
curl -s http://localhost:5100/api/rules/catalog | grep -o '"collections":\[[^]]*\]'
curl -s -X POST http://localhost:5100/api/rules/evaluate -H 'content-type: application/json' -d '{
  "modelType":"customer",
  "document":{ "rule": { "asAtLeastNSatisfied": { "spec": "is-large-order" }, "n": 1, "path": "orders" } },
  "model":{ "age":30, "isActive":true, "orderCount":2, "orders":[{ "total":150 },{ "total":20 }] }
}'
kill %1
```
Expected: catalog includes the `orders` collection; evaluate returns `"satisfied":true`.

- [ ] **Step 3: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample/Program.cs
git commit -m "feat(rules-sample): add an Order collection for higher-order rules"
```

---

## Phase C — Styling foundation

### Task C1: design tokens + shell/pane styling (remove inline styles)

**Files:**
- Create: `ui/apps/demo/src/styles/tokens.css`, `ui/apps/demo/src/styles/app.css`
- Modify: `ui/apps/demo/src/main.tsx`, `ui/apps/demo/src/App.tsx`, `ui/apps/demo/src/JsonPane.tsx`, `ui/apps/demo/src/EvaluatePane.tsx`
- Test: `ui/apps/demo/test/App.test.tsx` (adjust only if it asserted inline styles)

- [ ] **Step 1: Create `tokens.css`** — CSS custom properties with a dark-mode override. Concretely:

```css
:root {
  --bg: #ffffff; --surface: #f6f7f9; --border: #d8dce2; --text: #1c2024;
  --muted: #5b6470; --accent: #4a63e7; --accent-weak: #eaeefb;
  --danger: #d1435b; --ho: #2f9e6f; --radius: 8px; --space: 8px;
  --mono: ui-monospace, SFMono-Regular, Menlo, monospace;
  --sans: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #14171a; --surface: #1c2126; --border: #2c333b; --text: #e6e9ee;
    --muted: #9aa4b0; --accent: #8aa0ff; --accent-weak: #222a44;
    --danger: #ff6d86; --ho: #58c99a;
  }
}
```

- [ ] **Step 2: Create `app.css`** — the shell + pane + shared control classes using the tokens (`.app` 3-column grid, `.pane`, `.pane h2`, `.field`, `.control` for select/input/textarea, `.btn`, `.btn-danger`, `.json`, `.assertion`, responsive `@media (max-width: 900px)` → single column). Author the rules to render a readable, product-like UI. Import both stylesheets from `main.tsx`:

```tsx
import './styles/tokens.css';
import './styles/app.css';
```

- [ ] **Step 3: Convert the shell + panes to classes** — remove every inline `style={{…}}` from `App.tsx`, `JsonPane.tsx`, `EvaluatePane.tsx`; replace with the classes from `app.css` (e.g. `App`'s `<main style=…>` → `<main className="app">`; `JsonPane`'s `<pre>` → `className="json"`; `EvaluatePane`'s indented rows → a `.assertion` class with a CSS custom property for depth, `style={{ '--depth': row.depth } as CSSProperties}` is acceptable *only* for the dynamic depth value). `BuilderPane` is replaced wholesale in Phase D.

- [ ] **Step 4: Run tests**

Run: `pnpm -C ui/apps/demo test`
Expected: PASS (adjust `App.test.tsx` only if it asserted an inline style; keep behavioral assertions).

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/styles ui/apps/demo/src/main.tsx ui/apps/demo/src/App.tsx \
        ui/apps/demo/src/JsonPane.tsx ui/apps/demo/src/EvaluatePane.tsx ui/apps/demo/test/App.test.tsx
git commit -m "feat(rules-demo): vanilla-CSS design tokens; de-inline shell + panes"
```

---

## Phase D — Accordion builder

### Task D1: recursive accordion over the boolean grammar

**Files:**
- Create: `ui/apps/demo/src/builder/RuleNodeEditor.tsx`, `ui/apps/demo/src/builder/NodeToolbar.tsx`, `ui/apps/demo/src/builder/DecorationEditor.tsx`, `ui/apps/demo/src/builder/mutations.ts`, `ui/apps/demo/src/builder/childPaths.ts`
- Rewrite: `ui/apps/demo/src/BuilderPane.tsx` → `ui/apps/demo/src/panes/BuilderPane.tsx`
- Move: `ui/apps/demo/src/EvaluatePane.tsx`, `ui/apps/demo/src/JsonPane.tsx` → `ui/apps/demo/src/panes/` (update imports in `App.tsx`)
- Test: `ui/apps/demo/test/builder/RuleNodeEditor.test.tsx`

**Component contract (implement to these):**
- `mutations.ts` exports `toggleNot(store, path, node)` — if `isNotNode(node)` → `store.unwrap(path)`; else → `store.replaceNode(path, { not: node })`.
- `childPaths.ts` exports `childPaths(node, path): string[]` deriving child paths: NOT → `[`${path}.not`]`; binary → `operandsOf(node).map((_,i)=>`${path}.${binaryOperator(node)}[${i}]`)`; higher-order → `[`${path}.${higherOrderKey(node)}`]`; else `[]`. (Mirrors `listPaths` in rules-core.)
- `RuleNodeEditor` props: `{ path: string; depth: number }`. Reads its node via `useRuleNode(path)`. Renders a header row (`NodeToolbar`) always; when expanded, `DecorationEditor` + recursive `RuleNodeEditor` for each `childPaths`. Accordion state lives in `BuilderPane` (a `Set<string>` of expanded paths + a setter passed via context or props); single-open among siblings.
- `BuilderPane`: owns catalog (`useCatalog`), the store, and accordion state; seeds expanded paths from the root down to `MAX_EXPAND_DEPTH = 5` (compute from `listPaths(document)` depth). Renders the root `RuleNodeEditor`.

- [ ] **Step 1: Write the failing tests** — `ui/apps/demo/test/builder/RuleNodeEditor.test.tsx`:

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

describe('BuilderPane accordion (boolean)', () => {
  it('wraps a leaf in AND and shows two operands', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');
    fireEvent.click(screen.getByRole('button', { name: 'wrap $.rule in AND' }));
    const rule = store.getState().document.rule as { and?: unknown[] };
    expect(rule.and).toHaveLength(2);
  });

  it('toggles NOT on a leaf and back', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');
    fireEvent.click(screen.getByRole('button', { name: 'toggle NOT at $.rule' }));
    expect(store.getState().document.rule).toEqual({ not: { spec: 'is-active' } });
    fireEvent.click(screen.getByRole('button', { name: 'toggle NOT at $.rule' }));
    expect(store.getState().document.rule).toEqual({ spec: 'is-active' });
  });

  it('edits whenTrue decoration into the document', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');
    fireEvent.change(screen.getByLabelText('whenTrue at $.rule'), { target: { value: 'yes' } });
    expect((store.getState().document.rule as { whenTrue?: string }).whenTrue).toBe('yes');
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `pnpm -C ui/apps/demo test -- RuleNodeEditor`
Expected: FAIL (module `panes/BuilderPane` not found).

- [ ] **Step 3: Implement** the five files to the contract above. `NodeToolbar` renders, per node kind: for spec leaves a `<select aria-label={`spec at ${path}`}>` over `catalog.specs` (filtered by the node's model type — for the root that is the demo's `MODEL_TYPE`, `'customer'`), a `toggle NOT at {path}` button, a `wrap {path} in AND/OR/XOR/AndAlso/OrElse` menu (buttons or a `<select>`), an `add operand to {path}` button on binary nodes, and a `remove {path}` button on non-root nodes (`path !== '$.rule'`). `DecorationEditor` renders inputs `aria-label={`name|whenTrue|whenFalse at ${path}`}` writing through `setName`/`setDecoration`. Wire wrap via `store.wrapInOperator(path, op, { spec: catalog.specs[0]?.name ?? 'spec' })`, add-operand via `store.addOperand(path, { spec: … })`, remove via `store.removeOperand(path)`, NOT via `toggleNot`.

- [ ] **Step 4: Run to verify they pass**

Run: `pnpm -C ui/apps/demo test -- RuleNodeEditor && pnpm -C ui/apps/demo build`
Expected: PASS + build clean (`App.tsx` now imports panes from `./panes/…` and `BuilderPane` reads `catalogState.data.specs`).

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder ui/apps/demo/src/panes ui/apps/demo/src/App.tsx ui/apps/demo/test/builder
git rm ui/apps/demo/src/BuilderPane.tsx
git commit -m "feat(rules-demo): single-open accordion builder over the boolean grammar"
```

---

### Task D2: `QuantifierNode` (higher-order)

**Files:**
- Create: `ui/apps/demo/src/builder/QuantifierNode.tsx`
- Modify: `ui/apps/demo/src/builder/mutations.ts`, `ui/apps/demo/src/builder/RuleNodeEditor.tsx`, `ui/apps/demo/src/builder/NodeToolbar.tsx`
- Test: `ui/apps/demo/test/builder/QuantifierNode.test.tsx`

- [ ] **Step 1: Write the failing tests** — `QuantifierNode.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [
    { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
    { name: 'is-large-order', modelType: 'order', metadataType: 'String', isAsync: false, description: null },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('QuantifierNode', () => {
  it('inserts an at-least-N quantifier over a collection with an element-scoped child', async () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-adult' } });
    renderWith(store);
    await screen.findByLabelText('spec at $.rule');

    fireEvent.click(screen.getByRole('button', { name: 'wrap $.rule in AND' }));       // AND[is-adult, is-adult]
    fireEvent.click(screen.getByRole('button', { name: 'add quantifier to $.rule' })); // AND[..., quantifier]

    const q = store.getState().document.rule as { and: Array<Record<string, unknown>> };
    const quant = q.and[q.and.length - 1];
    // default kind = asAllSatisfied over the first collection, child = first element-model spec
    expect(quant).toMatchObject({ asAllSatisfied: { spec: 'is-large-order' }, path: 'orders' });
  });

  it('changes kind to at-least-N, sets n, and scopes the child picker to element specs', async () => {
    const store = new RuleEditorStore({
      rule: { asAllSatisfied: { spec: 'is-large-order' }, path: 'orders' },
    });
    renderWith(store);

    fireEvent.change(await screen.findByLabelText('quantifier kind at $.rule'), { target: { value: 'asAtLeastNSatisfied' } });
    fireEvent.change(screen.getByLabelText('quantifier n at $.rule'), { target: { value: '2' } });

    const rule = store.getState().document.rule as Record<string, unknown>;
    expect(rule).toMatchObject({ asAtLeastNSatisfied: { spec: 'is-large-order' }, n: 2, path: 'orders' });

    // the child spec picker only offers element-model (order) specs
    const childSelect = screen.getByLabelText('spec at $.rule.asAtLeastNSatisfied') as HTMLSelectElement;
    const options = Array.from(childSelect.options).map((o) => o.value);
    expect(options).toEqual(['is-large-order']);
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `pnpm -C ui/apps/demo test -- QuantifierNode`
Expected: FAIL.

- [ ] **Step 3: Implement**
  - `mutations.ts`: add `insertQuantifier(store, operatorPath, catalog)` — builds a default higher-order node `{ [firstKind]: { spec: firstElementSpec }, path: firstCollection.path }` (kind default `asAllSatisfied`; `firstElementSpec` = first `catalog.specs` whose `modelType === firstCollection.elementModelType`) and calls `store.addOperand(operatorPath, node)`. Also `setQuantifierKind(store, path, node, kind)` — rebuild the node moving the child under the new key and adding/removing `n` (default `1` when switching to an N-kind, drop `n` for all/any) via `replaceNode`; `setQuantifierCollection`/`setQuantifierN` similarly via `replaceNode`.
  - `NodeToolbar`: on binary nodes, add an `add quantifier to {path}` button (calls `insertQuantifier`) alongside `add operand`.
  - `RuleNodeEditor`: when `isHigherOrderNode(node)`, render `QuantifierNode` instead of the plain header — showing `quantifier kind at {path}` (`<select>` of the five kinds), `quantifier collection at {path}` (`<select>` over `catalog.collections` paths), `quantifier n at {path}` (number input, only for the three N-kinds), the "for each {elementModelType}" band, and the recursive child `RuleNodeEditor` at `${path}.${higherOrderKey(node)}` whose spec `<select>` is filtered to `catalog.specs` matching the collection's `elementModelType`. Collapsed, render the badge (e.g. `≥{n} of {path}` / `all of {path}`).
  - Thread the element model type down so the child's `NodeToolbar` spec `<select>` filters correctly (pass an optional `modelType` prop to `RuleNodeEditor`; default the demo `MODEL_TYPE`; a quantifier sets it to the collection's `elementModelType` for its child subtree).

- [ ] **Step 4: Run to verify they pass**

Run: `pnpm -C ui/apps/demo test -- QuantifierNode && pnpm -C ui/apps/demo build`
Expected: PASS + build clean.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder ui/apps/demo/test/builder/QuantifierNode.test.tsx
git commit -m "feat(rules-demo): higher-order quantifier node over registered collections"
```

---

### Task D3: disabled expression + parameter extension points

**Files:**
- Modify: `ui/apps/demo/src/builder/NodeToolbar.tsx`, `ui/apps/demo/src/panes/BuilderPane.tsx`
- Test: `ui/apps/demo/test/builder/ExtensionPoints.test.tsx`

- [ ] **Step 1: Write the failing test** — assert a disabled control exists and edits nothing:

```tsx
it('shows expression and parameters as disabled extension points', async () => {
  const store = new RuleEditorStore({ rule: { spec: 'is-adult' } });
  renderWith(store);
  await screen.findByLabelText('spec at $.rule');
  const expr = screen.getByRole('button', { name: /expression .*coming/i });
  expect(expr).toBeDisabled();
  const params = screen.getByRole('button', { name: /parameters .*coming/i });
  expect(params).toBeDisabled();
});
```

- [ ] **Step 2: Run to verify it fails** — `pnpm -C ui/apps/demo test -- ExtensionPoints`. Expected: FAIL.

- [ ] **Step 3: Implement** — in `NodeToolbar`, add a `<button disabled title="requires backend (coming)">expression — coming</button>` on leaf nodes; in `BuilderPane` header, a `<button disabled>parameters — coming</button>`. Both are inert (no onClick).

- [ ] **Step 4: Run to verify it passes** — `pnpm -C ui/apps/demo test -- ExtensionPoints`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ui/apps/demo/src/builder/NodeToolbar.tsx ui/apps/demo/src/panes/BuilderPane.tsx ui/apps/demo/test/builder/ExtensionPoints.test.tsx
git commit -m "feat(rules-demo): expression + parameters as disabled extension points"
```

---

## Phase E — Run ergonomics

### Task E1: one-command run script + Makefile + launch.json + README

**Files:**
- Create: `run-demo.sh` (repo root), `Makefile` (or add a target if one exists)
- Modify: `.claude/launch.json` (stage the existing file), `ui/apps/demo/README.md`

- [ ] **Step 1: Create `run-demo.sh`** (repo root, `chmod +x`):

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
echo "==> Building the UI"
pnpm -C ui install --frozen-lockfile
pnpm -C ui/apps/demo build
echo "==> Starting the host on http://localhost:5100"
exec dotnet run --project src/examples/Motiv.RulesEngine.Sample --urls http://localhost:5100
```

- [ ] **Step 2: Makefile target** — add (create the file if absent):

```makefile
.PHONY: demo
demo:
	./run-demo.sh
```

- [ ] **Step 3: Commit `.claude/launch.json`** — it already launches `rules-demo` on 5100; stage it so the in-editor preview is part of the reference.

- [ ] **Step 4: Rewrite `ui/apps/demo/README.md`** — lean: what it is; `./run-demo.sh` (or `make demo`); `docker compose up` (Task E2); a short "Extend it" pointer to the seam comments and the disabled expression/parameter extension points. No walkthrough.

- [ ] **Step 5: Verify** — `bash -n run-demo.sh` (syntax) and confirm `./run-demo.sh` starts the host (Ctrl-C after it serves; or reuse the Phase B curl check).

- [ ] **Step 6: Commit**

```bash
chmod +x run-demo.sh
git add run-demo.sh Makefile .claude/launch.json ui/apps/demo/README.md
git commit -m "feat(rules-demo): one-command run script, Makefile target, launch.json, lean README"
```

---

### Task E2: Docker + compose

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/Dockerfile`, `docker-compose.yml` (repo root), `.dockerignore` (repo root)

- [ ] **Step 1: Multi-stage `Dockerfile`** — stage 1 (node): `corepack enable`, copy `ui/`, `pnpm install --frozen-lockfile`, `pnpm -C apps/demo build` (outputs into the host `wwwroot` via the demo's vite `outDir`; ensure the build context includes both `ui/` and `src/`). Stage 2 (dotnet sdk): copy the repo, `dotnet publish src/examples/Motiv.RulesEngine.Sample -c Release -o /app` (with the built `wwwroot` present). Stage 3 (aspnet runtime): copy `/app`, `ENV ASPNETCORE_URLS=http://+:5100`, `EXPOSE 5100`, `ENTRYPOINT ["dotnet","Motiv.RulesEngine.Sample.dll"]`. Author the exact stages against the repo layout; keep the node and dotnet base image tags at the versions the repo targets (Node 20, .NET 10).

- [ ] **Step 2: `docker-compose.yml`** (repo root):

```yaml
services:
  demo:
    build:
      context: .
      dockerfile: src/examples/Motiv.RulesEngine.Sample/Dockerfile
    ports:
      - "5100:5100"
```

- [ ] **Step 3: `.dockerignore`** — exclude `**/bin`, `**/obj`, `**/node_modules`, `**/dist`, `.git`, `.claude`, `.superpowers`.

- [ ] **Step 4: Verify** — `docker build -f src/examples/Motiv.RulesEngine.Sample/Dockerfile -t motiv-demo .` succeeds and `docker run -p 5100:5100 motiv-demo` serves the app (if Docker is unavailable in the environment, `docker build` at minimum must be attempted; if it cannot run, note that in the report and leave the files for the user to verify).

- [ ] **Step 5: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample/Dockerfile docker-compose.yml .dockerignore
git commit -m "feat(rules-demo): dockerfile + compose for one-command containerized run"
```

---

## Phase F — Tests + final verification

### Task F1: Playwright E2E over the full higher-order flow

**Files:**
- Modify: `ui/apps/demo/e2e/smoke.spec.ts` (or add `e2e/higher-order.spec.ts`)

- [ ] **Step 1: Write the E2E** — building the rule via the accordion and evaluating it. Drive: wait for `spec at $.rule`; wrap root in AND; on the second operand click `add quantifier to $.rule`; set `quantifier kind` = `asAtLeastNSatisfied`, `quantifier n` = `1`, collection = `orders`, child spec = `is-large-order`; set the first operand spec to `is-adult`; set the sample-model textarea to a customer with an order ≥ 100; click Evaluate; assert `outcome` contains `Satisfied`. Use the existing spec's structure and the `getByLabel`/`getByRole` selectors matching the aria-labels from Phase D.

- [ ] **Step 2: Run** — `pnpm -C ui/apps/demo e2e` (builds SPA + starts host + real Chromium). Expected: PASS. If Chromium isn't installed: `pnpm -C ui/apps/demo exec playwright install chromium` first.

- [ ] **Step 3: Commit**

```bash
git add ui/apps/demo/e2e
git commit -m "test(rules-demo): E2E building and evaluating a higher-order rule"
```

---

### Task F2: full-solution verification + code-simplifier + final review

**Files:** none (verification)

- [ ] **Step 1: Full multi-TFM build** — `dotnet build`. Expected: 0 warnings / 0 errors (all TFMs, incl. net472).
- [ ] **Step 2: Full .NET suite on net10** — `dotnet test --framework net10.0`. Expected: all green, incl. the example projects that assert justification strings.
- [ ] **Step 3: UI** — `pnpm -C ui -r build` and `pnpm -C ui -r test` (rules-core, rules-react, demo). Expected: green.
- [ ] **Step 4: Run the app and confirm visually** — start via `./run-demo.sh` (or the `rules-demo` preview), build `is-adult AND (≥1 of orders is-large-order)`, evaluate, and screenshot the styled result. Confirm no console errors.
- [ ] **Step 5: Code-simplifier review** (mandatory per CLAUDE.md) over the new builder files (`builder/*`, `panes/BuilderPane.tsx`) and the backend catalog changes. Apply worthwhile suggestions; re-run `pnpm -C ui/apps/demo test`.
- [ ] **Step 6: Commit any review changes**

```bash
git add -A
git commit -m "refactor(rules-demo): simplify builder per review"
```

---

## Self-Review notes (already reconciled)

- **Spec coverage:** collection discovery (A1–A4) ✔; sample host (B1) ✔; styling/tokens/de-inline (C1) ✔; accordion + boolean grammar (D1) ✔; quantifier node + element-scoped child (D2) ✔; disabled expression/parameter extension points (D3) ✔; run script/Makefile/launch.json/README (E1) + Docker (E2) ✔; E2E + full verify (F1–F2) ✔.
- **Contract ripple:** the `/catalog` fold is delivered atomically for consumers — A2 (backend) → A3 (rules-core) → A4 (rules-react); the demo app is only made to compile against the new shape in Phase D (`BuilderPane` reads `catalogState.data.specs`), which is where `BuilderPane` is rewritten anyway.
- **Naming consistency:** `Catalog` / `CatalogCollection` (TS), `CatalogResponse` / `CatalogCollection` (C#), `CollectionRegistryEntry` (C#), `insertQuantifier` / `toggleNot` / `childPaths` (demo), aria-labels `spec at {path}` / `wrap {path} in {OP}` / `toggle NOT at {path}` / `add operand to {path}` / `add quantifier to {path}` / `quantifier {kind|collection|n} at {path}` / `{name|whenTrue|whenFalse} at {path}` — used identically in components and tests.
- **Model-space scoping:** the child of a quantifier binds against the element model; the demo enforces this in the UI by threading the collection's `elementModelType` into the child subtree's spec picker, matching the backend's element-space binding.
