# Result Serialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `ResultSerializer` to `Motiv.Serialization` that projects a Motiv evaluation result (`BooleanResultBase<TMetadata>`) into a serializable JSON document (`satisfied` / `reason` / `assertions` / `values` / `justification` / a de-noised causal `explanation` tree), so a frontend can render *why* a rule passed or failed.

**Architecture:** A new pure, framework-free type in the existing `Motiv.Serialization` package. It has two responsibilities behind a small surface: `ToEvaluationResult<TMetadata>(...)` projects a live result into a plain DTO (`RuleEvaluationResult<TMetadata>` + `ExplanationNode`), and `Serialize<TMetadata>(...)` renders that DTO to a JSON string via `System.Text.Json` with camelCase naming. The projection is the unit-testable core; JSON is a thin outer layer. No evaluation happens here — callers pass an already-evaluated result, keeping this decoupled from any HTTP/host concern (that arrives in Plan 2).

**Tech Stack:** C# (net8.0/net9.0/net10.0/netstandard2.0), `System.Text.Json`, xUnit + Shouldly for tests. `ImplicitUsings` and `Nullable` are **enabled** and `TreatWarningsAsErrors` is **on** repo-wide — every public member needs an XML doc comment or the build fails.

---

## Context for the implementer (read first)

This plan lives in the `Motiv.Serialization` package (`src/Motiv.Serialization/`), which already loads JSON rule documents into specs. You are adding the *outbound* direction: turning an evaluation **result** into JSON. The frontend design spec (`docs/superpowers/specs/2026-07-16-rules-engine-frontend-ui-design.md`) calls this "Plan 1 — result serialization"; it is the first of four plans.

Key core types you will consume (all in the `Motiv` / `Motiv.Shared` namespaces, referenced via the existing `ProjectReference` to `Motiv`):

- **`BooleanResultBase<TMetadata>`** (`src/Motiv/BooleanResultBase.cs`) — the result of evaluating a spec. Public members you need:
  - `bool Satisfied`
  - `string Reason` — concise, operator-joined summary
  - `string Justification` — multi-line hierarchical string
  - `IEnumerable<string> Assertions` — flat, already de-duplicated
  - `IEnumerable<TMetadata> Values` — the metadata that determined the outcome
  - `Explanation Explanation` — the causal explanation object (below)
- **`Explanation`** (`src/Motiv/Shared/Explanation.cs`, namespace `Motiv.Shared`) — a node in the causal tree:
  - `IEnumerable<string> Assertions` — assertions at this node
  - `IEnumerable<Explanation> Underlying` — child explanations, **already de-noised** (causes only). Use `Underlying`, *not* `AllUnderlying`.
- **Evaluation API:** `SpecBase<TModel, string>.Evaluate(model)` returns `BooleanResultBase<string>`; `SpecBase<TModel, TMetadata>.Evaluate(model)` returns `BooleanResultBase<TMetadata>`. Build specs with `Spec.Build(...).WhenTrue(...).WhenFalse(...).Create(...)`.

Conventions to match (see `src/Motiv.Serialization/RuleError.cs`): **plain sealed classes with a constructor and get-only properties** — no records, no `init` accessors (they are not polyfilled for `netstandard2.0`/`net472` in the production packages). Test conventions (see `src/Motiv.Serialization.Tests/RuleSerializerDeserializeTests.cs`): xUnit `[Fact]`, Shouldly `ShouldBe`, `// Arrange / Act / Assert` comments, `static` spec fixtures on the test class.

**Robust-assertion rule:** for `Reason`/`Justification`/`Assertions` in composition tests, do **not** hardcode Motiv's exact operator-string formatting (it is an internal detail and changes). Instead evaluate the spec live and compare the DTO field to the result's own property (`dto.Reason.ShouldBe(result.Reason)`). Only hardcode literal strings for the simplest single-leaf explanation cases where the assertion text is unambiguous (e.g. `"is positive"`).

**File structure (all under `src/`):**
- Create `src/Motiv.Serialization/RuleEvaluationResult.cs` — the two DTO types.
- Create `src/Motiv.Serialization/ResultSerializer.cs` — the projector + JSON serializer.
- Create `src/Motiv.Serialization.Tests/ResultSerializerTests.cs` — the tests.

No `.csproj` changes are needed: `System.Text.Json` is in-framework for net8+ and already a `PackageReference` for `netstandard2.0` in `Motiv.Serialization.csproj`.

**Test commands** (run from repo root). Restrict to this test class to keep the loop fast; run the full suite before the final commit:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```

---

## Task 1: Evaluation-result DTOs

**Files:**
- Create: `src/Motiv.Serialization/RuleEvaluationResult.cs`

There is no behavior to test yet — this task only introduces the data types the later tasks project into. It is verified by compilation (the first test in Task 2 will not compile until these exist).

- [ ] **Step 1: Create the DTO file**

Create `src/Motiv.Serialization/RuleEvaluationResult.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A serializable projection of a Motiv evaluation result, suitable for returning across an
/// HTTP boundary to a rules-engine frontend so it can render why a rule passed or failed.
/// </summary>
/// <typeparam name="TMetadata">The metadata type carried by the evaluated result.</typeparam>
public sealed class RuleEvaluationResult<TMetadata>
{
    /// <summary>Creates an evaluation-result projection.</summary>
    /// <param name="satisfied">Whether the rule was satisfied.</param>
    /// <param name="reason">The concise, operator-joined reason.</param>
    /// <param name="assertions">The flat, de-duplicated contributing assertions.</param>
    /// <param name="values">The metadata values that determined the outcome.</param>
    /// <param name="justification">The multi-line hierarchical justification string.</param>
    /// <param name="explanation">The de-noised causal explanation tree.</param>
    public RuleEvaluationResult(
        bool satisfied,
        string reason,
        IReadOnlyList<string> assertions,
        IReadOnlyList<TMetadata> values,
        string justification,
        ExplanationNode explanation)
    {
        Satisfied = satisfied;
        Reason = reason;
        Assertions = assertions;
        Values = values;
        Justification = justification;
        Explanation = explanation;
    }

    /// <summary>Whether the rule was satisfied.</summary>
    public bool Satisfied { get; }

    /// <summary>The concise, operator-joined reason for the outcome.</summary>
    public string Reason { get; }

    /// <summary>The flat, de-duplicated list of contributing assertions.</summary>
    public IReadOnlyList<string> Assertions { get; }

    /// <summary>The metadata values that determined the outcome.</summary>
    public IReadOnlyList<TMetadata> Values { get; }

    /// <summary>The multi-line hierarchical justification string.</summary>
    public string Justification { get; }

    /// <summary>The de-noised causal explanation tree.</summary>
    public ExplanationNode Explanation { get; }
}

/// <summary>A node in the de-noised causal explanation tree of an evaluation result.</summary>
public sealed class ExplanationNode
{
    /// <summary>Creates an explanation node.</summary>
    /// <param name="assertions">The assertions at this node.</param>
    /// <param name="underlying">The underlying (causal) explanation nodes.</param>
    public ExplanationNode(IReadOnlyList<string> assertions, IReadOnlyList<ExplanationNode> underlying)
    {
        Assertions = assertions;
        Underlying = underlying;
    }

    /// <summary>The assertions at this node.</summary>
    public IReadOnlyList<string> Assertions { get; }

    /// <summary>The underlying (causal) explanation nodes.</summary>
    public IReadOnlyList<ExplanationNode> Underlying { get; }
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
dotnet build src/Motiv.Serialization/Motiv.Serialization.csproj
```
Expected: build succeeds (no warnings — every public member is documented).

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Serialization/RuleEvaluationResult.cs
git commit -m "feat: add evaluation-result DTOs for Motiv.Serialization"
```

---

## Task 2: Project scalar result fields

**Files:**
- Create: `src/Motiv.Serialization/ResultSerializer.cs`
- Test: `src/Motiv.Serialization.Tests/ResultSerializerTests.cs`

Projects everything except the explanation tree. The explanation is filled with an empty placeholder node here and implemented for real in Task 3.

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Serialization.Tests/ResultSerializerTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class ResultSerializerTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, string> IsEven { get; } =
        Spec.Build((int n) => n % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();

    [Fact]
    public void Should_project_scalar_fields_from_a_leaf_result()
    {
        // Arrange
        var result = IsPositive.Evaluate(5);

        // Act
        var dto = new ResultSerializer().ToEvaluationResult(result);

        // Assert
        dto.Satisfied.ShouldBe(result.Satisfied);
        dto.Reason.ShouldBe(result.Reason);
        dto.Assertions.ShouldBe(result.Assertions.ToArray());
        dto.Values.ShouldBe(result.Values.ToArray());
        dto.Justification.ShouldBe(result.Justification);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: FAILS to compile — `ResultSerializer` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `src/Motiv.Serialization/ResultSerializer.cs`:

```csharp
using System.Text.Json;
using Motiv.Shared;

namespace Motiv.Serialization;

/// <summary>
/// Projects Motiv evaluation results (<see cref="BooleanResultBase{TMetadata}"/>) into serializable
/// <see cref="RuleEvaluationResult{TMetadata}"/> documents, and renders them to JSON.
/// </summary>
public sealed class ResultSerializer
{
    private static readonly JsonSerializerOptions DefaultJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>Creates a result serializer.</summary>
    /// <param name="jsonOptions">
    /// Options used when rendering to JSON (property naming and metadata <c>values</c> serialization).
    /// When omitted, camelCase property naming is used.
    /// </param>
    public ResultSerializer(JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? DefaultJsonOptions;
    }

    /// <summary>Projects an evaluation result into a serializable document.</summary>
    /// <typeparam name="TMetadata">The metadata type carried by the result.</typeparam>
    /// <param name="result">The evaluated result to project.</param>
    /// <returns>A serializable projection of <paramref name="result"/>.</returns>
    public RuleEvaluationResult<TMetadata> ToEvaluationResult<TMetadata>(BooleanResultBase<TMetadata> result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));

        return new RuleEvaluationResult<TMetadata>(
            result.Satisfied,
            result.Reason,
            result.Assertions.ToArray(),
            result.Values.ToArray(),
            result.Justification,
            new ExplanationNode([], []));
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/ResultSerializer.cs \
        src/Motiv.Serialization.Tests/ResultSerializerTests.cs
git commit -m "feat: project scalar evaluation-result fields"
```

---

## Task 3: Map the de-noised explanation tree

**Files:**
- Modify: `src/Motiv.Serialization/ResultSerializer.cs`
- Test: `src/Motiv.Serialization.Tests/ResultSerializerTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `ResultSerializerTests` (the recursive helper compares the DTO tree to the live `Explanation` tree, so it stays robust to formatting changes):

```csharp
    [Fact]
    public void Should_map_the_denoised_causal_explanation_tree()
    {
        // Arrange
        var result = IsPositive.And(IsEven).Evaluate(4);

        // Act
        var dto = new ResultSerializer().ToEvaluationResult(result);

        // Assert
        ShouldMirror(dto.Explanation, result.Explanation);
    }

    private static void ShouldMirror(ExplanationNode node, Motiv.Shared.Explanation explanation)
    {
        node.Assertions.ShouldBe(explanation.Assertions.ToArray());

        var underlying = explanation.Underlying.ToArray();
        node.Underlying.Count.ShouldBe(underlying.Length);
        for (var i = 0; i < underlying.Length; i++)
            ShouldMirror(node.Underlying[i], underlying[i]);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: `Should_map_the_denoised_causal_explanation_tree` FAILS — `dto.Explanation.Underlying` is empty (placeholder), but the live result has two underlying nodes.

- [ ] **Step 3: Replace the placeholder with real mapping**

In `src/Motiv.Serialization/ResultSerializer.cs`, replace this line inside `ToEvaluationResult`:

```csharp
            new ExplanationNode([], []));
```

with:

```csharp
            MapExplanation(result.Explanation));
```

and add this private method to the class (after `ToEvaluationResult`):

```csharp
    private static ExplanationNode MapExplanation(Explanation explanation) =>
        new(
            explanation.Assertions.ToArray(),
            explanation.Underlying.Select(MapExplanation).ToArray());
```

(`Explanation` resolves via the existing `using Motiv.Shared;` at the top of the file.)

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: PASS (both tests).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/ResultSerializer.cs \
        src/Motiv.Serialization.Tests/ResultSerializerTests.cs
git commit -m "feat: map de-noised causal explanation tree"
```

---

## Task 4: Render to JSON with camelCase names

**Files:**
- Modify: `src/Motiv.Serialization/ResultSerializer.cs`
- Test: `src/Motiv.Serialization.Tests/ResultSerializerTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `ResultSerializerTests` (add `using System.Text.Json;` to the top of the test file alongside the existing `using Motiv.Serialization;`):

```csharp
    [Fact]
    public void Should_serialize_to_camelcase_json_with_the_expected_shape()
    {
        // Arrange
        var result = IsPositive.Evaluate(5);

        // Act
        var json = new ResultSerializer().Serialize(result);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        root.GetProperty("reason").GetString().ShouldBe("is positive");
        root.GetProperty("assertions")[0].GetString().ShouldBe("is positive");
        root.GetProperty("justification").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("explanation").GetProperty("assertions")[0].GetString().ShouldBe("is positive");
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: FAILS to compile — `Serialize` does not exist.

- [ ] **Step 3: Add the `Serialize` method**

In `src/Motiv.Serialization/ResultSerializer.cs`, add after `ToEvaluationResult` (before `MapExplanation`):

```csharp
    /// <summary>Projects an evaluation result and renders it to a JSON string.</summary>
    /// <typeparam name="TMetadata">The metadata type carried by the result.</typeparam>
    /// <param name="result">The evaluated result to serialize.</param>
    /// <returns>The JSON representation of the projected result.</returns>
    public string Serialize<TMetadata>(BooleanResultBase<TMetadata> result) =>
        JsonSerializer.Serialize(ToEvaluationResult(result), _jsonOptions);
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: PASS (three tests).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/ResultSerializer.cs \
        src/Motiv.Serialization.Tests/ResultSerializerTests.cs
git commit -m "feat: render evaluation result to camelCase JSON"
```

---

## Task 5: Lock in typed (non-string) metadata values

**Files:**
- Test: `src/Motiv.Serialization.Tests/ResultSerializerTests.cs`

The DTO is generic; this proves `values` serialize as their real type (e.g. integers), not stringified — guarding the seam that later metadata-rule support (backend Plan 3) depends on. No production change expected; if the test fails, fix the projection.

- [ ] **Step 1: Write the failing test**

Add a fixture and test to `ResultSerializerTests`:

```csharp
    private static SpecBase<int, int> HasFlag { get; } =
        Spec.Build((int n) => n != 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("has flag");

    [Fact]
    public void Should_serialize_typed_metadata_values_as_their_real_type()
    {
        // Arrange
        var result = HasFlag.Evaluate(5);
        var serializer = new ResultSerializer();

        // Act
        var dto = serializer.ToEvaluationResult(result);
        var json = serializer.Serialize(result);

        // Assert
        dto.Values.ShouldBe(new[] { 1 });
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("values")[0].GetInt32().ShouldBe(1);
    }
```

- [ ] **Step 2: Run the test to verify it passes (or fails meaningfully)**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj \
  --filter "FullyQualifiedName~ResultSerializerTests"
```
Expected: PASS — the generic projection already handles typed metadata. (If it fails, the projection is wrong; fix `ToEvaluationResult` before continuing.)

- [ ] **Step 3: Run the full package test suite across all target frameworks**

Per project convention, do not trust a single-framework run — netfx breaks hide in net10-only runs.

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj
```
Expected: PASS on net8.0, net9.0, net472, and net10.0.

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Serialization.Tests/ResultSerializerTests.cs
git commit -m "test: verify typed metadata values serialize as their real type"
```

---

## Task 6: Mandatory post-implementation simplification review

**Files:**
- Review: `src/Motiv.Serialization/RuleEvaluationResult.cs`, `src/Motiv.Serialization/ResultSerializer.cs`, `src/Motiv.Serialization.Tests/ResultSerializerTests.cs`

CLAUDE.md requires a `code-simplifier` review after implementation. This is not optional.

- [ ] **Step 1: Spawn the code-simplifier agent**

Dispatch the `code-simplifier:code-simplifier` agent, pointing it at the three files created/modified in this plan. Ask it to focus on duplication, convoluted design, and naming — while preserving all functionality and the public surface (`ResultSerializer`, `RuleEvaluationResult<TMetadata>`, `ExplanationNode`).

- [ ] **Step 2: Apply any accepted suggestions and re-run tests**

If the agent proposes changes, apply them, then run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj
```
Expected: PASS on all target frameworks.

- [ ] **Step 3: Commit any changes**

```bash
git add src/Motiv.Serialization/ src/Motiv.Serialization.Tests/
git commit -m "refactor: simplify result serializer per review"
```

(If the agent found nothing to change, skip this commit.)

---

## Self-Review Notes (author)

- **Spec coverage:** This plan implements exactly the "Result serialization (in `Motiv.Serialization`)" section of the frontend spec — the `satisfied` / `reason` / `assertions` / `values` / `justification` / `explanation`-tree DTO, with the tree mirroring Motiv's de-noised causal `Explanation.Underlying`. The OpenAPI contract, `MapMotivRules`, model registration, and catalog `description` are **Plan 2**, not here. The TypeScript packages are Plans 3–4. Documentation is bundled into the frontend spec's final plan; no user-facing docs are added here (this DTO is an internal outbound projection consumed by Plan 2).
- **Deferred by design:** no untyped `BooleanResultBase` overload (every caller has a `TMetadata`; YAGNI); no result *deserialization* (the frontend only reads results — DTOs are serialize-only, so get-only properties are correct); no wiring into `RuleSerializerOptions` (the ASP.NET package passes `JsonSerializerOptions` directly in Plan 2).
- **Type consistency:** `ResultSerializer`, `RuleEvaluationResult<TMetadata>`, `ExplanationNode`, `ToEvaluationResult`, `Serialize`, and `MapExplanation` are named identically everywhere they appear across tasks.
