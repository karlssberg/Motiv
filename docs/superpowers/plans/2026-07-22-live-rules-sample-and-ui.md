# Live Rules Sample & UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The sample app executes live rules (`POST /api/checkout` with a sync + an async rule), and the React demo can pick a server rule, edit it, save it with optimistic concurrency, see conflicts, and watch the checkout outcome change — proving hot-swap end to end.

**Architecture:** Sample declares sealed rule classes (compiled + document defaults) wired via `AddMotivRules(...).AddRule<T>()`; the checkout endpoint receives handles by type through minimal-API DI. `@motiv/rules-core` gains `listRules`/`getRule`/`putRule`/`revertRule` (409 → typed conflict, not a throw); the demo gains a rule picker + save/conflict header and a checkout pane.

**Tech Stack:** C# minimal API (`src/examples/Motiv.RulesEngine.Sample`), TypeScript (`ui/packages/rules-core`, vitest), React (`ui/apps/demo`, vitest + Testing Library, Playwright e2e).

**Prerequisite:** Plan 3 (`2026-07-22-rule-handles-and-endpoints.md`).

**Test commands:**

```bash
DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test *.sln -v minimal
```

```bash
cd ui && pnpm install && pnpm -r test
```

---

## Context for the implementing engineer

- **Sample today:** `src/examples/Motiv.RulesEngine.Sample/Program.cs` builds a `SpecRegistry` (specs `is-active`, `is-adult`, `has-orders`, `is-large-order`; collection `orders`), `MotivRulesOptions` with models `customer`/`order`, and calls `app.MapMotivRules("/api/rules", registry, options)`. Models: `Customer(int Age, bool IsActive, int OrderCount, IReadOnlyList<Order>? Orders)`, `Order(decimal Total)`.
- **UI today:** `ui/apps/demo/src/App.tsx` owns a `RuleEditorStore` (initial doc `{ rule: { spec: 'is-active' } }`) + `RulesApiClient` + debounced validation; panes: `BuilderPane`, `JsonPane`, `EvaluatePane`. `RuleEditorStore` (`ui/packages/rules-core/src/editor.ts`) has `getState/subscribe/replaceNode/...` but **no whole-document replace** — the picker needs one.
- **Client style:** `ui/packages/rules-core/src/client.ts` — `#post`/`#read` helpers, `RulesApiError` for non-2xx. Follow it; 409 must NOT throw (typed result instead).
- **Serialized result shape:** `ResultSerializer.ToEvaluationResult(result)` → `RuleEvaluationResult` (camelCase via `options.JsonSerializerOptions`) — the TS `EvaluationResult` type mirrors it.
- **Vite dev proxy:** `ui/apps/demo/vite.config.ts` proxies `/api` to the sample host; production build outputs to the sample's `wwwroot`.
- **e2e:** `ui/apps/demo/e2e/*.spec.ts` + `playwright.config.ts` (drives the built app against the running sample — read `ui/apps/demo/README.md` for the run recipe before writing the e2e task).

### File map

| File | Responsibility |
|---|---|
| Create `src/examples/Motiv.RulesEngine.Sample/AppRules.cs` | sealed rule classes + default documents |
| Create `src/examples/Motiv.RulesEngine.Sample/Rules/loyalty-discount.json` | embedded document default |
| Modify `src/examples/Motiv.RulesEngine.Sample/Program.cs` | async spec, DI wiring, `/api/checkout` |
| Modify `src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj` | embedded resource |
| Create `src/examples/Motiv.RulesEngine.Sample.Tests/*` | checkout + rules endpoint integration tests |
| Modify `ui/packages/rules-core/src/contracts.ts` | rule endpoint types + new error codes |
| Modify `ui/packages/rules-core/src/client.ts` | `listRules`/`getRule`/`putRule`/`revertRule` |
| Modify `ui/packages/rules-core/src/editor.ts` | `loadDocument()` whole-document replace |
| Modify `ui/packages/rules-core/src/index.ts` | exports |
| Create `ui/apps/demo/src/panes/RuleHeader.tsx` | picker + save + conflict banner |
| Create `ui/apps/demo/src/panes/CheckoutPane.tsx` | live checkout demonstration |
| Modify `ui/apps/demo/src/App.tsx` | wire header + pane |
| Tests | `ui/packages/rules-core/test/client.test.ts`, `.../editor.test.ts`, `ui/apps/demo/test/panes/RuleHeader.test.tsx`, `.../CheckoutPane.test.tsx`, `ui/apps/demo/e2e/live-rules.spec.ts` |

---

### Task 1: Sample rules + `/api/checkout`

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/AppRules.cs`
- Create: `src/examples/Motiv.RulesEngine.Sample/Rules/loyalty-discount.json`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj`
- Create: `src/examples/Motiv.RulesEngine.Sample.Tests/Motiv.RulesEngine.Sample.Tests.csproj`
- Create: `src/examples/Motiv.RulesEngine.Sample.Tests/CheckoutEndpointTests.cs`

- [ ] **Step 1: Create the test project** (mirror `src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj` — copy its csproj, retarget the `ProjectReference` to the sample project, keep xUnit/Shouldly/TestServer packages; add it to the solution with `dotnet sln add`). The sample must expose its `Program` to tests — append to `Program.cs`:

```csharp
/// <summary>Exposes the entry point to WebApplicationFactory-based integration tests.</summary>
public partial class Program;
```

and reference `Microsoft.AspNetCore.Mvc.Testing` in the test csproj.

- [ ] **Step 2: Write the failing tests**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Motiv.RulesEngine.Sample.Tests;

public class CheckoutEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CheckoutEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Should_approve_an_active_adult_customer()
    {
        // Arrange
        var client = _factory.CreateClient();
        var customer = new { age = 30, isActive = true, orderCount = 3, orders = new[] { new { total = 120m } } };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", customer);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("approved").GetBoolean().ShouldBeTrue();
        body.GetProperty("eligibility").GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        body.GetProperty("screening").GetProperty("satisfied").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_reject_an_inactive_customer_with_reasons()
    {
        // Arrange
        var client = _factory.CreateClient();
        var customer = new { age = 30, isActive = false, orderCount = 0 };

        // Act
        var body = await (await client.PostAsJsonAsync("/api/checkout", customer))
            .Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        body.GetProperty("approved").GetBoolean().ShouldBeFalse();
        body.GetProperty("eligibility").GetProperty("assertions").EnumerateArray()
            .Select(a => a.GetString()).ShouldContain("customer is inactive");
    }

    [Fact]
    public async Task Should_reflect_a_rule_swap_on_the_next_checkout()
    {
        // Arrange — swap can-checkout to require orders, then re-run the same customer
        var factory = new WebApplicationFactory<Program>();   // isolated host: mutates rule state
        var client = factory.CreateClient();
        var customer = new { age = 30, isActive = true, orderCount = 0 };
        (await (await client.PostAsJsonAsync("/api/checkout", customer))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approved").GetBoolean().ShouldBeTrue();
        var document = JsonDocument.Parse(
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "has-orders" } ] } }""").RootElement;

        // Act
        var put = await client.PutAsJsonAsync("/api/rules/rules/can-checkout", new { document, baseVersion = 1 });
        var after = await (await client.PostAsJsonAsync("/api/checkout", customer))
            .Content.ReadFromJsonAsync<JsonElement>();

        // Assert — no restart, next call sees the new rule
        put.EnsureSuccessStatusCode();
        after.GetProperty("approved").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_list_the_three_sample_rules()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert
        var names = body.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToArray();
        names.ShouldBe(["can-checkout", "fraud-screening", "loyalty-discount"], ignoreOrder: true);
    }
}
```

- [ ] **Step 3: Run to verify failure** (`/api/checkout` 404s):

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -v minimal`
Expected: FAIL.

- [ ] **Step 4: Implement.**

`src/examples/Motiv.RulesEngine.Sample/Rules/loyalty-discount.json` (embedded; also add `<EmbeddedResource Include="Rules\loyalty-discount.json" />` to the csproj):

```json
{
  "rule": {
    "and": [ { "spec": "is-active" }, { "spec": "has-orders" } ],
    "name": "qualifies for loyalty discount"
  }
}
```

`src/examples/Motiv.RulesEngine.Sample/AppRules.cs`:

```csharp
using Motiv;
using Motiv.Serialization;

/// <summary>
/// The application's live rules. Each rule is a sealed class so the type itself is the
/// identity — inject the concrete type wherever the rule is executed. Defaults are either
/// compiled specs (refactor-safe, no document until first save) or embedded rule documents
/// (authored/exported from the UI, served from version 1).
/// </summary>
public static class DefaultSpecs
{
    /// <summary>The compiled default for <see cref="CanCheckoutRule"/>: active AND adult.</summary>
    public static SpecBase<Customer, string> CanCheckout { get; } =
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active").WhenFalse("customer is inactive").Create()
            .AndAlso(Spec.Build((Customer c) => c.Age >= 18)
                .WhenTrue("customer is an adult").WhenFalse("customer is a minor").Create());

    /// <summary>The compiled default for <see cref="FraudScreeningRule"/>: the simulated credit check.</summary>
    public static AsyncSpecBase<Customer, string> FraudScreening { get; } =
        Spec.BuildAsync(async (Customer c, CancellationToken ct) =>
            {
                // Simulated credit-bureau latency; replace with a real client call.
                await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
                return c.OrderCount >= 0 && c.Age >= 18;
            })
            .WhenTrue("passes credit check")
            .WhenFalse("fails credit check")
            .Create();
}

/// <summary>Gate for the checkout flow: hot-swappable via PUT /api/rules/rules/can-checkout.</summary>
public sealed class CanCheckoutRule() : Rule<Customer, string>(
    "can-checkout", DefaultSpecs.CanCheckout, "Gate for the checkout flow");

/// <summary>Async screening rule demonstrating an async spec composed into a live rule.</summary>
public sealed class FraudScreeningRule() : AsyncRule<Customer, string>(
    "fraud-screening", DefaultSpecs.FraudScreening, "Simulated credit-bureau screening");

/// <summary>Document-default rule: the embedded JSON is the version-1 implementation.</summary>
public sealed class LoyaltyDiscountRule() : Rule<Customer, string>(
    "loyalty-discount", RuleDocuments.Embedded("loyalty-discount.json"),
    "Whether the customer qualifies for the loyalty discount");
```

`Program.cs` changes — register the async spec, switch to DI wiring, add the checkout endpoint (keep the existing registry/collection/options code; replace the mapping section):

```csharp
// Seam: async specs register like sync ones; documents referencing them load via async rules.
registry.Register(
    "passes-credit-check",
    Spec.BuildAsync(async (Customer c, CancellationToken ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
            return c.Age >= 18;
        })
        .WhenTrue("passes credit check")
        .WhenFalse("fails credit check")
        .Create(),
    "Simulated async credit-bureau check");

var builder = WebApplication.CreateBuilder(args);

// Seam: live rules. Each AddRule enrolls a sealed rule class as a DI singleton and in the
// RuleSet behind GET/PUT/DELETE /api/rules/rules — the app executes the same instances the
// UI hot-swaps, with optimistic-concurrency protection on writes.
builder.Services.AddMotivRules(registry, options)
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapMotivRules("/api/rules");

// Seam: a rule being *used*. Handles arrive by type via DI — no name strings, and each
// Evaluate/EvaluateAsync reads an immutable snapshot, so a concurrent PUT never tears a result.
var resultSerializer = new ResultSerializer();
app.MapPost("/api/checkout", async (
    CanCheckoutRule canCheckout,
    FraudScreeningRule fraudScreening,
    Customer customer,
    CancellationToken cancellationToken) =>
{
    var eligibility = canCheckout.Evaluate(customer);
    var screening = await fraudScreening.EvaluateAsync(customer, cancellationToken);
    return Results.Json(new CheckoutResponse(
        eligibility.Satisfied && screening.Satisfied,
        resultSerializer.ToEvaluationResult(eligibility),
        resultSerializer.ToEvaluationResult(screening)),
        options.JsonSerializerOptions);
});

app.MapFallbackToFile("index.html");

app.Run();

/// <summary>The outcome of a checkout attempt: both live rules and the combined verdict.</summary>
public sealed record CheckoutResponse(
    bool Approved,
    RuleEvaluationResult Eligibility,
    RuleEvaluationResult Screening);

/// <summary>Exposes the entry point to WebApplicationFactory-based integration tests.</summary>
public partial class Program;
```

(Add `using Motiv.Serialization;` for `ResultSerializer`/`RuleEvaluationResult`. Check `ResultSerializer.ToEvaluationResult`'s parameter type accepts both results — it takes a `BooleanResultBase<string>`; the async result is one too.)

- [ ] **Step 5: Run the tests** → PASS. **Step 6: Commit**

```bash
git add src/examples
git commit -m "feat: sample executes live sync+async rules via /api/checkout"
```

---

### Task 2: rules-core — contracts + client methods

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts`
- Modify: `ui/packages/rules-core/src/client.ts`
- Modify: `ui/packages/rules-core/src/index.ts`
- Modify: `ui/packages/rules-core/test/client.test.ts`

- [ ] **Step 1: Write the failing tests** (append to `client.test.ts`, following its existing mock-fetch pattern — read the file first and reuse its helpers):

```typescript
describe('rule endpoints', () => {
  it('lists rules', async () => {
    const entries = [{ name: 'can-checkout', modelType: 'customer', metadataType: 'String',
      isAsync: false, isPolicy: false, version: 1, description: null }];
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: okJson(entries) });
    expect(await client.listRules()).toEqual(entries);
  });

  it('gets a rule document with its version', async () => {
    const body = { document: { rule: { spec: 'is-active' } }, version: 3 };
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: okJson(body) });
    expect(await client.getRule('can-checkout')).toEqual(body);
  });

  it('puts a rule and returns the updated version', async () => {
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: okJson({ version: 2 }) });
    const result = await client.putRule('can-checkout', { rule: { spec: 'is-active' } }, 1);
    expect(result).toEqual({ outcome: 'updated', version: 2 });
  });

  it('returns a typed conflict instead of throwing on 409', async () => {
    const client = new RulesApiClient({
      baseUrl: '/api/rules',
      fetch: jsonResponse(409, { currentVersion: 4 }),
    });
    const result = await client.putRule('can-checkout', { rule: { spec: 'is-active' } }, 1);
    expect(result).toEqual({ outcome: 'conflict', currentVersion: 4 });
  });

  it('returns typed validation errors instead of throwing on 400', async () => {
    const errors = [{ path: '$.rule', code: 'UnknownSpec', message: 'x' }];
    const client = new RulesApiClient({
      baseUrl: '/api/rules',
      fetch: jsonResponse(400, { errors }),
    });
    const result = await client.putRule('can-checkout', { rule: { spec: 'nope' } }, 1);
    expect(result).toEqual({ outcome: 'invalid', errors });
  });

  it('reverts a rule via delete with the base version in the query', async () => {
    let requested: string | undefined;
    const fetchSpy: typeof fetch = async (input, init) => {
      requested = String(input);
      expect(init?.method).toBe('DELETE');
      return new Response(JSON.stringify({ version: 5 }), { status: 200 });
    };
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchSpy });
    const result = await client.revertRule('can-checkout', 4);
    expect(requested).toBe('/api/rules/rules/can-checkout?baseVersion=4');
    expect(result).toEqual({ outcome: 'updated', version: 5 });
  });
});
```

(`okJson`/`jsonResponse` are illustrative — if `client.test.ts` already has equivalent response-stub helpers, use those; otherwise define them locally in the test file: `const jsonResponse = (status: number, body: unknown): typeof fetch => async () => new Response(JSON.stringify(body), { status });` and `const okJson = (body: unknown) => jsonResponse(200, body);`.)

- [ ] **Step 2: Run to verify failure:** `cd ui && pnpm --filter @motiv/rules-core test` → FAIL (methods missing).

- [ ] **Step 3: Implement.** Append to `contracts.ts`:

```typescript
/** One live-rule listing from GET /rules. */
export interface RuleListEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  isPolicy: boolean;
  version: number;
  description?: string | null;
}

/** A rule's current document (null while on a code-defined default) and version. */
export interface RuleGetResponse {
  document: RuleDocument | null;
  version: number;
}

/** The outcome of a save or revert: updated, a version conflict, or validation errors. */
export type RuleSaveResult =
  | { outcome: 'updated'; version: number }
  | { outcome: 'conflict'; currentVersion: number }
  | { outcome: 'invalid'; errors: RuleError[] };
```

and extend the `RuleErrorCode` union with `'AsyncSpecInHigherOrder' | 'PolicyRequired'`.

Append to `RulesApiClient`:

```typescript
  /** GET {baseUrl}/rules */
  async listRules(): Promise<RuleListEntry[]> {
    const response = await this.#fetch(`${this.#baseUrl}/rules`, { method: 'GET' });
    return this.#read<RuleListEntry[]>(response);
  }

  /** GET {baseUrl}/rules/{name} */
  async getRule(name: string): Promise<RuleGetResponse> {
    const response = await this.#fetch(`${this.#baseUrl}/rules/${encodeURIComponent(name)}`, { method: 'GET' });
    return this.#read<RuleGetResponse>(response);
  }

  /** PUT {baseUrl}/rules/{name} — 409/400 return typed outcomes rather than throwing. */
  async putRule(name: string, document: RuleDocument, baseVersion: number): Promise<RuleSaveResult> {
    const response = await this.#fetch(`${this.#baseUrl}/rules/${encodeURIComponent(name)}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ document, baseVersion }),
    });
    return this.#readSaveResult(response);
  }

  /** DELETE {baseUrl}/rules/{name}?baseVersion=N — reverts to the rule's default. */
  async revertRule(name: string, baseVersion: number): Promise<RuleSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/rules/${encodeURIComponent(name)}?baseVersion=${baseVersion}`,
      { method: 'DELETE' },
    );
    return this.#readSaveResult(response);
  }

  async #readSaveResult(response: Response): Promise<RuleSaveResult> {
    if (response.ok) {
      const body = (await response.json()) as { version: number };
      return { outcome: 'updated', version: body.version };
    }
    if (response.status === 409) {
      const body = (await response.json()) as { currentVersion: number };
      return { outcome: 'conflict', currentVersion: body.currentVersion };
    }
    if (response.status === 400) {
      const body = (await response.json()) as ValidationResponse;
      return { outcome: 'invalid', errors: body.errors };
    }
    return this.#read<never>(response);   // 404 etc. → RulesApiError as elsewhere
  }
```

Export the new types from `index.ts` (match its existing export style).

- [ ] **Step 4: Run** `pnpm --filter @motiv/rules-core test` → PASS. **Step 5: Commit**

```bash
git add ui/packages/rules-core
git commit -m "feat(rules-core): rule endpoints client with typed save outcomes"
```

---

### Task 3: rules-core — `RuleEditorStore.loadDocument`

**Files:**
- Modify: `ui/packages/rules-core/src/editor.ts`
- Modify: `ui/packages/rules-core/test/editor.test.ts`

- [ ] **Step 1: Write the failing tests** (append):

```typescript
describe('loadDocument', () => {
  it('replaces the whole document and clears history and errors', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    store.replaceNode('$.rule', { spec: 'b' });
    store.setErrors([{ path: '$.rule', code: 'UnknownSpec', message: 'x' }]);

    store.loadDocument({ rule: { spec: 'c' } });

    const state = store.getState();
    expect(state.document).toEqual({ rule: { spec: 'c' } });
    expect(state.errors).toEqual([]);
    expect(state.canUndo).toBe(false);   // a load is a fresh baseline, not an edit
    expect(state.canRedo).toBe(false);
  });

  it('notifies subscribers', () => {
    const store = new RuleEditorStore({ rule: { spec: 'a' } });
    let notified = 0;
    store.subscribe(() => { notified += 1; });

    store.loadDocument({ rule: { spec: 'b' } });

    expect(notified).toBe(1);
  });
});
```

- [ ] **Step 2: Run to verify failure**, then implement in `editor.ts` (after `setErrors`):

```typescript
  /** Replaces the entire document as a fresh baseline: history and errors are cleared. */
  loadDocument(document: RuleDocument): void {
    this.#document = structuredClone(document);
    this.#errors = [];
    this.#undo = [];
    this.#redo = [];
    this.#notify();
  }
```

- [ ] **Step 3: Run** `pnpm --filter @motiv/rules-core test` → PASS. **Step 4: Commit**

```bash
git add ui/packages/rules-core
git commit -m "feat(rules-core): loadDocument replaces the editor baseline"
```

---

### Task 4: Demo — rule picker, save, conflict banner (`RuleHeader`)

**Files:**
- Create: `ui/apps/demo/src/panes/RuleHeader.tsx`
- Create: `ui/apps/demo/test/panes/RuleHeader.test.tsx`
- Modify: `ui/apps/demo/src/App.tsx`
- Modify: `ui/apps/demo/src/styles/app.css` (reuse existing `.pane`, `.btn` classes; add only what's missing)

- [ ] **Step 1: Write the failing tests** (follow the render/mocking style of the existing `ui/apps/demo/test/panes/EvaluatePane.test.tsx` — read it first; stub the client with plain objects):

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { RuleHeader } from '../../src/panes/RuleHeader.js';

const entries = [
  { name: 'can-checkout', modelType: 'customer', metadataType: 'String',
    isAsync: false, isPolicy: false, version: 1, description: 'Gate' },
];

function makeClient(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    listRules: vi.fn().mockResolvedValue(entries),
    getRule: vi.fn().mockResolvedValue({ document: { rule: { spec: 'is-active' } }, version: 3 }),
    putRule: vi.fn().mockResolvedValue({ outcome: 'updated', version: 4 }),
    ...overrides,
  } as never;
}

function renderHeader(client: never, store = new RuleEditorStore({ rule: { spec: 'is-active' } })) {
  render(
    <RuleEditorProvider store={store}>
      <RuleHeader client={client} />
    </RuleEditorProvider>,
  );
  return store;
}

describe('RuleHeader', () => {
  it('lists server rules and loads the picked one into the store', async () => {
    const client = makeClient();
    const store = renderHeader(client);

    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');

    await waitFor(() => expect(store.getState().document).toEqual({ rule: { spec: 'is-active' } }));
    expect(screen.getByText(/v3/)).toBeTruthy();
  });

  it('shows a code-default note when the server document is null', async () => {
    const client = makeClient({
      getRule: vi.fn().mockResolvedValue({ document: null, version: 1 }),
    });
    renderHeader(client);

    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');

    expect(await screen.findByText(/code-defined default/i)).toBeTruthy();
  });

  it('saves with the loaded version and shows the new one', async () => {
    const client = makeClient();
    renderHeader(client);
    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() =>
      expect(client.putRule).toHaveBeenCalledWith('can-checkout', { rule: { spec: 'is-active' } }, 3));
    expect(await screen.findByText(/v4/)).toBeTruthy();
  });

  it('shows a conflict banner with a reload action on version conflicts', async () => {
    const client = makeClient({
      putRule: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 9 }),
    });
    renderHeader(client);
    await userEvent.selectOptions(await screen.findByLabelText('Rule'), 'can-checkout');
    await screen.findByText(/v3/);

    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.getByText(/someone else saved version 9/i)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));
    await waitFor(() => expect(client.getRule).toHaveBeenCalledTimes(2));
  });
});
```

- [ ] **Step 2: Run to verify failure:** `cd ui && pnpm --filter demo test` → FAIL.

- [ ] **Step 3: Implement `RuleHeader.tsx`:**

```tsx
import { useEffect, useState } from 'react';
import type { RuleListEntry, RulesApiClient } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { useSyncExternalStore } from 'react';

/** The picked rule's server identity: what Save must send back to avoid clobbering. */
interface LoadedRule {
  name: string;
  version: number;
  isCodeDefault: boolean;
}

/**
 * Seam: dynamic replacement. Picks a live server rule, loads its document into the shared
 * editor store, and saves it back with the loaded version — a stale version surfaces as a
 * conflict banner (open two tabs to watch the race protection work).
 */
export function RuleHeader(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const document = useSyncExternalStore(
    (listener) => store.subscribe(listener),
    () => store.getState().document,
  );
  const [rules, setRules] = useState<RuleListEntry[]>([]);
  const [loaded, setLoaded] = useState<LoadedRule | null>(null);
  const [conflict, setConflict] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    props.client.listRules().then((entries) => {
      if (!cancelled) setRules(entries);
    });
    return () => { cancelled = true; };
  }, [props.client]);

  const load = async (name: string): Promise<void> => {
    if (!name) return setLoaded(null);
    const response = await props.client.getRule(name);
    setConflict(null);
    setLoaded({ name, version: response.version, isCodeDefault: response.document === null });
    if (response.document) store.loadDocument(response.document);
  };

  const save = async (): Promise<void> => {
    if (!loaded) return;
    setSaving(true);
    try {
      const result = await props.client.putRule(loaded.name, document, loaded.version);
      if (result.outcome === 'updated') {
        setConflict(null);
        setLoaded({ ...loaded, version: result.version, isCodeDefault: false });
      } else if (result.outcome === 'conflict') {
        setConflict(result.currentVersion);
      }
      // 'invalid' outcomes surface through the store's live validation errors already.
    } finally {
      setSaving(false);
    }
  };

  return (
    <header className="pane rule-header">
      <label>
        Rule
        <select value={loaded?.name ?? ''} onChange={(e) => void load(e.target.value)}>
          <option value="">— local draft —</option>
          {rules.map((rule) => (
            <option key={rule.name} value={rule.name}>{rule.name}</option>
          ))}
        </select>
      </label>
      {loaded && (
        <span className="rule-version">
          v{loaded.version}
          {loaded.isCodeDefault && <em> — code-defined default (builder starts fresh)</em>}
        </span>
      )}
      <button type="button" className="btn" disabled={!loaded || saving} onClick={() => void save()}>
        Save
      </button>
      {conflict !== null && loaded && (
        <div role="alert" className="conflict-banner">
          Someone else saved version {conflict} of “{loaded.name}”.
          <button type="button" className="btn" onClick={() => void load(loaded.name)}>
            Reload latest
          </button>
        </div>
      )}
    </header>
  );
}
```

Wire into `App.tsx` above the three panes:

```tsx
      <main className="app">
        <RuleHeader client={client} />
        <BuilderPane client={client} />
        <JsonPane />
        <EvaluatePane client={client} />
      </main>
```

(plus the import; add minimal `.rule-header`/`.conflict-banner`/`.rule-version` styles to `app.css` consistent with existing tokens.)

- [ ] **Step 4: Run** `pnpm --filter demo test` → PASS. **Step 5: Commit**

```bash
git add ui/apps/demo
git commit -m "feat(demo): rule picker with versioned save and conflict banner"
```

---

### Task 5: Demo — `CheckoutPane` (the rule being used)

**Files:**
- Create: `ui/apps/demo/src/panes/CheckoutPane.tsx`
- Create: `ui/apps/demo/test/panes/CheckoutPane.test.tsx`
- Modify: `ui/apps/demo/src/App.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CheckoutPane } from '../../src/panes/CheckoutPane.js';

const approval = {
  approved: true,
  eligibility: { satisfied: true, reason: 'r', assertions: ['customer is active'], values: [], justification: 'j', explanation: { assertions: [], underlying: [] } },
  screening: { satisfied: true, reason: 'r2', assertions: ['passes credit check'], values: [], justification: 'j2', explanation: { assertions: [], underlying: [] } },
};

describe('CheckoutPane', () => {
  afterEach(() => vi.restoreAllMocks());

  it('posts the sample customer to /api/checkout and renders both verdicts', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(approval), { status: 200 }));
    render(<CheckoutPane />);

    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(fetchSpy).toHaveBeenCalledWith('/api/checkout', expect.objectContaining({ method: 'POST' }));
    expect(await screen.findByText(/approved/i)).toBeTruthy();
    expect(screen.getByText('customer is active')).toBeTruthy();
    expect(screen.getByText('passes credit check')).toBeTruthy();
  });

  it('lets the user edit the sample customer JSON before trying', async () => {
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify({ ...approval, approved: false }), { status: 200 }));
    render(<CheckoutPane />);

    const input = screen.getByLabelText(/customer/i);
    await userEvent.clear(input);
    await userEvent.type(input, '{{"age": 15, "isActive": true, "orderCount": 0}');
    await userEvent.click(screen.getByRole('button', { name: /try checkout/i }));

    expect(await screen.findByText(/rejected/i)).toBeTruthy();
  });
});
```

(Adjust the `EvaluationResult` stub fields to the real `EvaluationResult` interface in `contracts.ts` — open it and mirror the exact property list.)

- [ ] **Step 2: Run to verify failure**, then implement `CheckoutPane.tsx`:

```tsx
import { useState } from 'react';
import type { EvaluationResult } from '@motiv/rules-core';

interface CheckoutResponse {
  approved: boolean;
  eligibility: EvaluationResult;
  screening: EvaluationResult;
}

const SAMPLE_CUSTOMER = `{
  "age": 30,
  "isActive": true,
  "orderCount": 3,
  "orders": [{ "total": 120 }]
}`;

/**
 * Seam: the rule being *used*. POST /api/checkout executes the live CanCheckoutRule (sync)
 * and FraudScreeningRule (async) on the server — save a rule change and the very next
 * checkout reflects it, no restart.
 */
export function CheckoutPane() {
  const [customerJson, setCustomerJson] = useState(SAMPLE_CUSTOMER);
  const [outcome, setOutcome] = useState<CheckoutResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const tryCheckout = async (): Promise<void> => {
    setError(null);
    try {
      const response = await fetch('/api/checkout', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: customerJson,
      });
      if (!response.ok) throw new Error(`Checkout failed (${response.status}).`);
      setOutcome((await response.json()) as CheckoutResponse);
    } catch (cause) {
      setOutcome(null);
      setError(cause instanceof Error ? cause.message : String(cause));
    }
  };

  return (
    <section className="pane" aria-label="Checkout">
      <div className="pane-header">
        <h2>Checkout (live rules)</h2>
      </div>
      <label>
        Customer
        <textarea rows={6} value={customerJson} onChange={(e) => setCustomerJson(e.target.value)} />
      </label>
      <button type="button" className="btn" onClick={() => void tryCheckout()}>
        Try checkout
      </button>
      {error && <p role="alert">{error}</p>}
      {outcome && (
        <div className="checkout-outcome">
          <strong>{outcome.approved ? 'Approved' : 'Rejected'}</strong>
          <Verdict title="Eligibility (sync rule)" result={outcome.eligibility} />
          <Verdict title="Screening (async rule)" result={outcome.screening} />
        </div>
      )}
    </section>
  );
}

function Verdict(props: { title: string; result: EvaluationResult }) {
  return (
    <div className="verdict">
      <h3>{props.title}</h3>
      <ul>
        {props.result.assertions.map((assertion) => (
          <li key={assertion}>{assertion}</li>
        ))}
      </ul>
    </div>
  );
}
```

Wire into `App.tsx` after `EvaluatePane`.

- [ ] **Step 3: Run** `pnpm --filter demo test` → PASS. **Step 4: Commit**

```bash
git add ui/apps/demo
git commit -m "feat(demo): checkout pane exercising the live rules"
```

---

### Task 6: e2e — hot-swap without restart, conflict on stale save

**Files:**
- Create: `ui/apps/demo/e2e/live-rules.spec.ts`

- [ ] **Step 1: Read `ui/apps/demo/README.md` + `playwright.config.ts`** for the run recipe (how the sample host + built UI are started for the existing e2e specs) and follow it exactly.

- [ ] **Step 2: Write the spec**

```typescript
import { expect, test } from '@playwright/test';

test('editing and saving a rule changes the next checkout, and stale saves conflict', async ({ page, request }) => {
  await page.goto('/');

  // Load the live rule into the builder.
  await page.getByLabel('Rule').selectOption('can-checkout');
  await expect(page.getByText(/v\d+/)).toBeVisible();

  // Baseline: the sample customer checks out.
  await page.getByRole('button', { name: /try checkout/i }).click();
  await expect(page.getByText('Approved')).toBeVisible();

  // Make the rule impossible: wrap in NOT via the builder’s node toolbar
  // (reuse the interaction helpers/selectors from e2e/higher-order.spec.ts — align
  // these selectors with the existing builder e2e interactions).
  await page.getByRole('button', { name: /not/i }).first().click();
  await page.getByRole('button', { name: /^save$/i }).click();
  await expect(page.getByText(/v\d+/)).toBeVisible();

  // The very next checkout reflects the swap — no restart happened.
  await page.getByRole('button', { name: /try checkout/i }).click();
  await expect(page.getByText('Rejected')).toBeVisible();

  // A writer holding a stale version gets a 409 (simulated second tab via the API).
  const stale = await request.put('/api/rules/rules/can-checkout', {
    data: { document: { rule: { spec: 'is-active' } }, baseVersion: 1 },
  });
  expect(stale.status()).toBe(409);

  // And the UI path shows the banner: save again with what is now a stale UI version.
  await request.put('/api/rules/rules/can-checkout', {
    data: {
      document: { rule: { spec: 'is-active' } },
      baseVersion: (await (await request.get('/api/rules/rules/can-checkout')).json()).version,
    },
  });
  await page.getByRole('button', { name: /^save$/i }).click();
  await expect(page.getByRole('alert')).toContainText(/someone else saved/i);
  await page.getByRole('button', { name: /reload latest/i }).click();
  await expect(page.getByRole('alert')).toBeHidden();
});
```

Adjust selectors to the real builder toolbar (open `ui/apps/demo/e2e/higher-order.spec.ts` and `src/builder/NodeToolbar.tsx` for the actual accessible names). Run the suite per the README recipe and fix selector drift from the actual first run's output.

- [ ] **Step 3: Run e2e** (per README, typically: build UI, start sample, `pnpm --filter demo e2e`). Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ui/apps/demo/e2e
git commit -m "test(demo): e2e proof of live rule hot-swap and conflict handling"
```

---

### Task 7: Docs + full verification + review

- [ ] **Step 1: Update `README.md`** — add a brief "Live rules" example under Core Features (a sealed `Rule` class, `AddMotivRules().AddRule<T>()`, `Evaluate`, and the PUT-to-swap story) mirroring the style of existing feature sections. Update `ui/apps/demo/README.md` with the new panes.
- [ ] **Step 2: Update `docs/`** — per project convention, add `docs/live-rules/index.md` + method pages following the existing structure (`docs/{feature}/index.md`, `toc.yml`, entries in `docs/toc.yml` and `docs/Overview.md`).
- [ ] **Step 3: Full verification** — `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test *.sln -v minimal` AND `cd ui && pnpm -r test` AND the e2e recipe → all PASS.
- [ ] **Step 4: Spawn the mandatory `code-simplifier` agent** over the sample + UI changes; apply accepted improvements; re-run affected tests.
- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: live rules feature documentation"
```
