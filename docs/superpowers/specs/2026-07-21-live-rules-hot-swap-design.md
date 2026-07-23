# Live Rules — Typed Handles, Hot-Swap, and Async Loading — Design

**Date:** 2026-07-21
**Status:** Approved (pending spec review)
**Scope:** `Motiv.Serialization` (async binding + rule handles), `Motiv.Serialization.AspNetCore`
(rule endpoints + DI), `Motiv.RulesEngine.Sample` (a rule actually being used), `@motiv/rules-core`
and `ui/apps/demo` (load/save/conflict flow).

## Problem

The rules-engine vertical slice proves the *editing* story but not the *usage* story:

- The sample host mounts `/catalog`, `/validate`, `/evaluate` — but `/evaluate` runs ad-hoc
  documents sent by the client. **No rule is ever stored server-side or executed by the app's
  own business logic.**
- Because no server-side rule instance exists, there is nothing to *replace*. The React demo
  edits a document that lives only in browser memory.
- `SpecRegistry` accepts async specs and `RuleBinder` rejects them with *"use
  DeserializeAsyncSpec"* — **a method that does not exist**. Async rule loading is anticipated
  but unimplemented.
- There is no first-class, compile-time-typed handle for "a named rule this app executes".
  Consumers would have to thread `SpecBase<TModel, TMetadata>` and a serializer around
  themselves, with the metadata type erased at every seam.

The goal: a rule can be **declared with its model and metadata types inline**, **executed by
app code** (sync or async, multi-value or single-value), and **replaced at runtime from the
React UI without race conditions** — with minimal composition-root ceremony.

## Design Decisions (settled during brainstorming)

| Axis | Decision |
|---|---|
| Async scope | Full: implement async document binding (`DeserializeAsyncSpec`) + async handles now |
| Packaging | Handles + `RuleSet` in `Motiv.Serialization` (DI-free); endpoints + DI glue in `Motiv.Serialization.AspNetCore` |
| Concurrency | Atomic snapshot swap for evaluators **and** version-CAS optimistic writes for editors |
| Declaration DX | Sealed rule class per rule (`class CanCheckoutRule() : Rule<Customer, string>(...)`), injected by type via DI — no statics, no keyed services, no name strings at use sites |
| Rule flavours | 2×2 mirroring core Motiv: `Rule` / `PolicyRule` × sync / async |
| Default implementation | Compiled `Spec`/`Policy` **or** a serialized rule document (e.g. embedded JSON file); both bind fail-fast at `RuleSet.Add` |
| Source generator | **Non-goal** — deferred; the document-default overload is the seam it would generate against |

## 1. Async document binding (`Motiv.Serialization`)

New `RuleSerializer` methods, named to match the error message that already promises them:

```csharp
AsyncSpecBase<TModel, string>    DeserializeAsyncSpec<TModel>(string json);
AsyncSpecBase<TModel, string>    DeserializeAsyncSpec<TModel>(string json, object? parameters);
AsyncSpecBase<TModel, string>    DeserializeAsyncSpec<TModel>(string json, IReadOnlyDictionary<string, object?>? parameters);
AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json);            // + parameter overloads
IReadOnlyList<RuleError>         ValidateAsyncSpec<TModel>(string json);
IReadOnlyList<RuleError>         ValidateAsyncSpec<TModel, TMetadata>(string json);
```

- A new `AsyncRuleBinder` (and a metadata variant) **mirrors** `RuleBinder` rather than
  sharing branches with it, per the project's avoid-over-DRYing convention.
- Sync spec leaves are lifted with `ToAsyncSpec()`; async leaves are used directly (the
  `AsyncSpecInSyncLoad` rejection applies only to the existing sync binders).
- Compositions use the existing `AsyncSpecBase` operators (`And`, `AndAlso`, `Or`, `OrElse`,
  `XOr`, `Not`).
- **Correction (found during planning):** core has no decorator builders over *existing* async
  specs — `Spec.Build(asyncSpec).WhenTrue(...)`/`.Create(name)` does not exist (only
  predicate-built `Spec.BuildAsync(...)` supports decoration). Binding named/decorated nodes in
  an async load therefore requires a **core prerequisite**: async decorator propositions
  (minimal `Create(name)`, explanation `WhenTrue`/`WhenFalse` strings, and metadata
  `WhenTrue`/`WhenFalse` payloads over an `AsyncSpecBase`), mirroring the sync
  `DecoratorProposition` fluent surface and reusing its result types. This is Plan 1.
- **Boundary:** core Motiv has no async higher-order propositions (`AsyncSpecBase` has no
  quantifiers). A higher-order subtree must therefore be **fully synchronous**: it binds via
  the existing sync path and the resulting spec is lifted with `ToAsyncSpec()`. An async spec
  leaf *inside* a higher-order subtree is reported with a new error code,
  **`RuleErrorCode.AsyncSpecInHigherOrder`**.
- `TMetadata == string` short-circuits to the explanation path, as the sync methods do.

## 2. Rule handles (`Motiv.Serialization`)

Four handle types mirroring core Motiv's 2×2 type system. Each takes its **initial
implementation as a compiled Spec/Policy** — constraint enforced at compile time — or as a
**serialized rule document** bound later at `RuleSet.Add`:

| Handle | Compiled default | `Evaluate` returns |
|---|---|---|
| `Rule<TModel, TMetadata>` | `SpecBase<TModel, TMetadata>` | `BooleanResultBase<TMetadata>` (multiple `Values`) |
| `PolicyRule<TModel, TMetadata>` | `PolicyBase<TModel, TMetadata>` | `PolicyResultBase<TMetadata>` (single `Value`) |
| `AsyncRule<TModel, TMetadata>` | `AsyncSpecBase<TModel, TMetadata>` | `ValueTask<BooleanResultBase<TMetadata>>` |
| `AsyncPolicyRule<TModel, TMetadata>` | `AsyncPolicyBase<TModel, TMetadata>` | `ValueTask<PolicyResultBase<TMetadata>>` |

- `PolicyRule` derives from `Rule` (shadowing `Evaluate` with the policy result, exactly as
  `PolicyBase` shadows `SpecBase`); likewise `AsyncPolicyRule : AsyncRule`. A policy-flavoured
  rule is usable wherever its spec-flavoured base is expected.
- Async handles mirror core's `ValueTask`-based evaluation (`perf!` change #78) and **forward**
  the underlying spec's `ValueTask` directly — a snapshot read plus delegation, never an
  `async`/`Task` wrapper — so evaluations that complete synchronously stay allocation-free.
  `EvaluateAsync(model, CancellationToken)` takes and passes through the cancellation token.
- The intended declaration pattern is a **sealed subclass per rule** — the type *is* the
  identity:

  ```csharp
  public sealed class CanCheckoutRule() : Rule<Customer, string>(
      "can-checkout",
      Spec.Build((Customer c) => c.IsActive)
          .WhenTrue("customer is active").WhenFalse("customer is inactive").Create()
          .AndAlso(...),
      "Gate for the checkout flow");

  public sealed class LoyaltyRule() : Rule<Customer, string>(
      "loyalty-discount",
      RuleDocuments.Embedded("loyalty-discount.json"));   // document default, bound at Add
  ```

  `RuleDocuments.Embedded(...)` is a small helper that reads an embedded-resource JSON
  document from the calling assembly (an overload taking raw JSON also exists). The base
  constructors record either `CompiledDefault(spec)` or `DocumentDefault(json)`; no binding
  happens in the constructor — a parameterless subclass constructor has no registry in scope.

### State & concurrency

Each handle owns one `volatile`-read immutable snapshot:

```
RuleState { string? DocumentJson; int Version; TBound Spec; }
```

- **Evaluators:** `Evaluate` does a single `Volatile.Read` of the snapshot and runs entirely
  against it. In-flight evaluations finish on the old version when a swap lands mid-flight;
  there is never a torn or half-bound state.
- **Editors:** replacement is `Interlocked.CompareExchange` keyed on the *expected version*.
  A stale expected version fails the CAS and reports a conflict — optimistic concurrency all
  the way down; the HTTP layer merely surfaces it.
- Version numbering starts at **1** (the default), incrementing per successful update.
- **Revert:** the handle always retains its default, so a rule can be reset to it (surfaced
  as `DELETE` in the HTTP layer). Revert is itself a CAS'd update that bumps the version —
  it is *not* a rollback to version 1's number, avoiding ABA confusion for editors.

### `RuleSet`

The one necessary composition seam, DI-free:

```csharp
var rules = new RuleSet(registry)                 // owns a RuleSerializer internally
    .Add(new CanCheckoutRule())
    .Add(new FraudScreeningRule());
```

- `Add` binds document defaults (and validates compiled ones need no binding) **immediately**
  — an invalid default document throws at startup, not first evaluation. Duplicate names throw.
- `RuleSet.Update(name, documentJson, expectedVersion)` performs **validate → bind → CAS
  publish** and returns a discriminated result: `Updated(newVersion)` |
  `VersionConflict(currentVersion)` | `Invalid(errors)` | `NotFound`. Expected outcomes are
  values, not exceptions.
- `RuleSet.Revert(name, expectedVersion)` follows the same contract.
- Binding dispatch is flavour-aware via a non-generic `RuleBase` with internal abstract
  `TryUpdate` / `TryRevert`: sync handles use `Deserialize<TModel, TMetadata>`, async handles
  use `DeserializeAsyncSpec<TModel, TMetadata>`.
- **Policy flavour + documents:** whether a document yields a policy is a runtime property of
  the bound tree (a decorated root, a leaf referencing a registered policy, or `orElse` of
  policies). `PolicyRule.TryUpdate` checks the bound root is a `PolicyBase` (async:
  `AsyncPolicyBase`) and reports a new error code **`RuleErrorCode.PolicyRequired`** otherwise.
- `RuleSet.Rules` enumerates `{name, modelType, metadataType, isAsync, isPolicy, version,
  description, documentJson?}` for the catalog/endpoints.

## 3. Endpoints & DI (`Motiv.Serialization.AspNetCore`)

`Motiv.Serialization` stays DI-framework-free. The ASP.NET package adds the glue:

```csharp
builder.Services.AddMotivRules(registry, options)   // registers RuleSet + options as singletons
    .AddRule<CanCheckoutRule>()                     // singleton under its own type + enrolled in RuleSet
    .AddRule<FraudScreeningRule>();

app.MapMotivRules("/api/rules");                    // resolves registry/options/RuleSet from DI
```

- `AddRule<T>` constrains `T : RuleBase, new()` (or accepts an instance overload), registers
  the rule as a singleton under its concrete type, and enrolls it in the `RuleSet`.
- `MapMotivRules` resolves the `RuleSet` **eagerly** at mapping time so invalid default
  documents fail at startup. The existing explicit-parameter overload
  (`MapMotivRules(path, registry, options)`) remains for non-DI use, gaining an optional
  `RuleSet` parameter.
- New endpoints (mounted only when a `RuleSet` is present), alongside the existing
  `/catalog`, `/validate`, `/evaluate`:

| Endpoint | Behaviour |
|---|---|
| `GET {base}/rules` | `[{ name, modelType, metadataType, isAsync, isPolicy, version, description }]` (model ids via `ResolveModelId`) |
| `GET {base}/rules/{name}` | `{ document, version }` — `document: null` while on a compiled default |
| `PUT {base}/rules/{name}` `{ document, baseVersion }` | `200 { version }` \| `409 { currentVersion }` stale `baseVersion` \| `400 { errors }` invalid document \| `404` unknown rule |
| `DELETE {base}/rules/{name}?baseVersion=N` | revert to default (query param — `DELETE` bodies are unreliable) — same `200/409/404` contract |

## 4. Sample app — a rule actually being used

`Motiv.RulesEngine.Sample` gains a real business endpoint that executes live rules through
minimal-API parameter injection — compile-time safe, no name strings at use sites:

```csharp
app.MapPost("/api/checkout", async (CanCheckoutRule canCheckout, FraudScreeningRule fraud, Customer customer) =>
{
    var eligibility = canCheckout.Evaluate(customer);       // sync rule
    var screening   = await fraud.EvaluateAsync(customer);  // async rule
    return Results.Json(new CheckoutResponse(
        eligibility.Satisfied && screening.Satisfied,
        Eligibility: resultSerializer.ToEvaluationResult(eligibility),
        Screening:   resultSerializer.ToEvaluationResult(screening)));
});
```

- `CanCheckoutRule : Rule<Customer, string>` — compiled default composing registered specs.
- `FraudScreeningRule : AsyncRule<Customer, string>` — references a new registered **async
  spec** `"passes-credit-check"` (simulated credit-bureau call via `Task.Delay`),
  demonstrating sync and async rules side by side.
- Both demo rules use `string` metadata so the existing builder UI stays payload-simple; the
  typed-metadata DX (`PolicyRule<Customer, SomeDecision>`) is exercised in unit tests.
- Saving a document from the UI changes the very next `/api/checkout` response — no restart,
  no torn evaluations.

## 5. React UI — load, save, conflict

- **`@motiv/rules-core`:** `listRules()`, `getRule(name)`, `putRule(name, document,
  baseVersion)`, `revertRule(name, baseVersion)` on `RulesApiClient`, plus contract types.
  A `409` surfaces as a typed `VersionConflict` result, not a thrown error.
- **Demo app:**
  - A **rule picker** loads a server rule into the existing builder (empty builder + a
    "code-defined default" note when `document: null`).
  - A **Save** button PUTs with the version loaded; success updates the tracked version.
  - A `409` shows a **conflict banner** with a "reload latest" action — open two tabs to
    watch the race protection live.
  - A small **checkout pane** posts a sample customer to `/api/checkout` and renders both
    justification trees, so the effect of a save is visible immediately.

## 6. Testing

TDD throughout; full solution suite (`dotnet test`) plus `ui` vitest and Playwright e2e.

- **Async binding:** leaf/composition/decoration/mixed-tree binding parity with the sync
  binder; `AsyncSpecInHigherOrder`; sync-subtree lifting; metadata variant; `TMetadata ==
  string` short-circuit.
- **Handles & `RuleSet`:** flavour typing (policy shadowed results), document vs compiled
  defaults, fail-fast `Add`, `PolicyRequired`, version-CAS conflict, revert semantics, and a
  concurrency test: evaluation racing an update always sees a coherent snapshot
  (old-or-new, never mixed).
- **Endpoints:** `GET/PUT/DELETE` happy paths, `409` on stale `baseVersion`, `400` with
  structured errors, `404`, eager startup failure on an invalid default document.
- **UI:** vitest for client methods + conflict handling; Playwright e2e: edit → save →
  checkout outcome changes; stale save → conflict banner.

## 7. Phasing

Four plans (the core decorator prerequisite surfaced during planning):

1. **Async decorator propositions (core)** — `Spec.Build(asyncSpec)` fluent surface: minimal
   `Create(name)`, explanation strings, metadata payloads; async proposition classes reusing
   the sync decorator result types.
2. **Async binding** — `AsyncRuleBinder` (+ metadata variant), `DeserializeAsyncSpec` /
   `ValidateAsyncSpec`, new error codes.
3. **Handles + endpoints** — `Rule`/`PolicyRule`/`AsyncRule`/`AsyncPolicyRule`, `RuleState`
   swap machinery, `RuleSet`, `RuleDocuments`, DI extensions, rule endpoints.
4. **Sample + UI** — sample rules + `/api/checkout`, rules-core client methods, demo picker /
   save / conflict / checkout pane, e2e.

## Non-Goals

- **Source generator / build tool** for validating or embedding rule documents at compile
  time. Deferred: leaf references and type binding only resolve against the runtime registry,
  so build-time checking is limited to JSON structure; the document-default constructor
  overload is exactly the seam a future generator would target.
- Async higher-order propositions in core Motiv (prerequisite for async specs *inside*
  quantifier subtrees).
- Rule persistence beyond process lifetime (a database/file store behind `RuleSet` is a
  natural follow-on; the demo intentionally resets to defaults on restart).
- Multi-node coordination (distributed cache/bus fan-out of rule updates).
- Editing non-string metadata payloads in the demo UI.
