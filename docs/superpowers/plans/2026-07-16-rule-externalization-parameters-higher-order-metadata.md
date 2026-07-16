# Rule Externalization Plan 2: Parameters, Higher-Order, Metadata Binding — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `Motiv.Serialization` with the remaining v1 document vocabulary: load-time parameters (`@param` in `n`, `{param}` interpolation in payload strings), the five higher-order nodes (`asAllSatisfied`, `asAnySatisfied`, `asNSatisfied`, `asAtLeastNSatisfied`, `asAtMostNSatisfied`) with `path` support, and typed metadata loads via `Deserialize<TModel, TMetadata>` — plus the Plan-1 review carryovers (depth-formula clamp, options range validation, `$schema` / parameter-`default` strictness).

**Architecture:** The pipeline grows two stages between parse and bind: parameter **resolution** (merge supplied values over document defaults, type-check, error on missing/surplus) and **substitution** (rewrite `{param}` interpolation in payload strings and resolve `@param` references in `n` slots on the parsed `RuleNode` tree). Higher-order nodes bind by discovering the element type from the model (or the `path` property chain) via reflection, binding the inner tree with `MakeGenericMethod`, building the higher-order spec through the fluent builders, and re-anchoring to the document model with `ChangeModelTo`. Metadata loads get a parallel `MetadataRuleBinder<TMetadata>` that keeps registry specs typed at `TMetadata`; nodes carrying object payloads re-metadatize their subtree, which is bound with explanation (string) semantics by the existing `RuleBinder`. JSON remains a projection of the fluent builder — every equivalence test compares a JSON-loaded spec against its exact fluent-built twin.

**Tech Stack:** C# (LangVersion latest, nullable enabled), System.Text.Json, xUnit + Shouldly, JsonSchema.Net (test-only).

**Design spec:** `docs/superpowers/specs/2026-07-15-rule-externalization-design.md`
**Branch:** `feature/rule-externalization-plan2` (from `main`)

## Global Constraints

- Library TFMs: `net8.0;net9.0;netstandard2.0;net10.0`. Test TFMs: `net8.0;net9.0;net472;net10.0`. No new csproj changes are needed this plan.
- `TreatWarningsAsErrors` is on solution-wide; `Nullable` and `ImplicitUsings` are enabled via `Directory.Build.props`; central package management — never put `Version=` in a csproj.
- No records or `required`/`init` members in library code (`netstandard2.0` has no polyfills). Constructors + `get`-only or `get`/`set` properties. Primary constructors and collection expressions are fine (language-only features).
- Test metadata classes must be plain classes with `get`/`set` properties and a parameterless constructor — records break on net472 (`IsExternalInit`) and STJ on netstandard-era targets needs a settable shape.
- XML doc headers on all public types and members.
- TDD: write the failing test, see it fail, implement, see it pass, commit. Fast loop on `-f net10.0`; before each commit run the full serialization test matrix **including an explicit net472 run**:
  `dotnet test src/Motiv.Serialization.Tests -f net10.0` then `dotnet test src/Motiv.Serialization.Tests -f net472`.
- Error codes are a stable public contract — **append-only**. New members are appended after `DocumentTooLarge` in exactly this order: `MissingParameter`, `SurplusParameter`, `ParameterTypeMismatch`, `UnknownParameterReference`, `InvalidHigherOrderPath`.
- Error `Path` values in tests must match this plan exactly.
- The final task runs the **whole solution** test suite and the mandatory `code-simplifier` review.

## Semantics Decisions (locked in this plan)

These are loader semantics the design spec left open; tests encode them:

1. **Bare higher-order nodes need a statement.** A higher-order node with neither `name` nor string payloads is a parse error (`InvalidNode`) — the fluent builders require `Create("name")` there, and the loader never invents text.
2. **Object payloads require `name` on the same node.** Metadata propositions require an explicit statement in core (`Create("name")`); the parser enforces this structurally so `Validate(json)` catches it.
3. **Re-metadatized subtrees bind with explanation semantics.** When a node carries object payloads, its operator subtree (including registry leaves of any metadata type) is bound as `SpecBase<TModel, string>` via the existing `RuleBinder`, then decorated with `Spec.Build(spec).WhenTrue(obj).WhenFalse(obj).Create(name)`. Nested object payloads *inside* such a subtree are `MetadataTypeMismatch` errors.
4. **`Deserialize<TModel, string>` behaves exactly like `Deserialize<TModel>`** (explanation load) — it delegates.
5. **Interpolation applies only to `whenTrue`/`whenFalse` strings** (not `name`s, not object payloads). `{{`/`}}` escape literal braces; an unmatched `{` or `}` is `InvalidNode`; a payload that becomes empty/whitespace after interpolation is `InvalidNode`.
6. **Parameter formatting is invariant-culture; booleans render lowercase** (`true`/`false`).
7. **`integer` parameters are 32-bit.** Defaults or supplied values outside `int` range are errors.
8. **Semantic `Validate<TModel>`/`Validate<TModel, TMetadata>` take no parameter values.** Required parameters are stood in by placeholders (integer `0`, number `0`, boolean `false`, string → the parameter's own name) so binding can proceed; supply errors (`MissingParameter`/`SurplusParameter`/`ParameterTypeMismatch`) are only reported by `Deserialize`.
9. **Binding in `Validate<...>` only runs when the document has no structural errors** — parser errors can leave partial trees the binder must not walk.

## File Structure

```
schemas/
  rule.v1.json                                  (Task 11 — payload-name + higher-order-name constraints)
src/Motiv.Serialization/
  RuleSerializerOptions.cs                      (Task 1: range validation; Task 9: MetadataJsonOptions)
  RuleDocumentParser.cs                         (Task 1: clamp/$schema; Task 2: declarations; Task 5: higher-order; Task 8: payload retention)
  RuleDocument.cs                               (Task 2: Parameters list)
  RuleParameterType.cs                          (Task 2 — internal enum)
  RuleParameterDeclaration.cs                   (Task 2 — internal)
  RuleParameterResolver.cs                      (Task 3 — internal: merge/type-check/placeholders)
  RuleParameterSubstituter.cs                   (Task 4: interpolation; Task 6: n resolution)
  RuleErrorCode.cs                              (Task 3, 4, 7 — appended codes)
  RuleSerializer.cs                             (Task 3: parameter overloads; Task 9: metadata loads; Task 10: semantic Validate)
  RuleOperator.cs                               (Task 5 — five higher-order members)
  RuleNode.cs                                   (Task 5: N/NParameterName/PathText; Task 8: payload elements)
  HigherOrderModelResolution.cs                 (Task 6 — internal)
  HigherOrderModelResolver.cs                   (Task 6: element discovery; Task 7: path walking)
  RuleBinder.cs                                 (Task 6: higher-order; Task 7: path; Task 9: BindOperator split)
  MetadataRuleBinder.cs                         (Task 9 — internal, generic)
src/Motiv.Serialization.Tests/
  RuleSerializerOptionsTests.cs                 (Task 1)
  RuleSerializerValidateTests.cs                (Task 1: $schema; Task 5: higher-order structure)
  RuleParameterTests.cs                         (Task 2: defaults; Task 3: resolution; Task 4: interpolation)
  RuleHigherOrderTests.cs                       (Task 6: binding; Task 7: path)
  RuleMetadataTests.cs                          (Task 9)
  RuleSemanticValidateTests.cs                  (Task 10)
  RuleSchemaTests.cs                            (Task 11 — moved/added rows)
  SpecAssertions.cs                             (Task 9 — metadata overload)
```

---

### Task 1: Carryover hardening — depth clamp, options validation, `$schema` kind

**Files:**
- Modify: `src/Motiv.Serialization/RuleSerializerOptions.cs`
- Modify: `src/Motiv.Serialization/RuleDocumentParser.cs` (lines 26 and 57)
- Create: `src/Motiv.Serialization.Tests/RuleSerializerOptionsTests.cs`
- Modify: `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`

**Interfaces:**
- Consumes: existing `RuleSerializer.Validate(string)`, `RuleError`, `RuleErrorCode.InvalidNode`.
- Produces: `RuleSerializerOptions.MaxDocumentDepth` / `MaxNodeCount` setters that throw `ArgumentOutOfRangeException` below 1. No other public-surface change.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/RuleSerializerOptionsTests.cs`:

```csharp
namespace Motiv.Serialization.Tests;

public class RuleSerializerOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxDocumentDepth_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxDocumentDepth = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxNodeCount_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxNodeCount = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_validate_without_overflowing_when_MaxDocumentDepth_is_int_MaxValue()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxDocumentDepth = int.MaxValue };
        var serializer = new RuleSerializer(new SpecRegistry(), options);

        // Act
        var errors = serializer.Validate("""{ "rule": { "spec": "a" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }
}
```

Add to `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs` (reuse the file's existing `Validate` helper):

```csharp
    [Fact]
    public void Should_report_a_non_string_schema_reference()
    {
        // Act
        var errors = Validate("""{ "$schema": 1, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.$schema");
        error.Message.ShouldContain("string");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSerializerOptionsTests|FullyQualifiedName~Should_report_a_non_string_schema_reference"`
Expected: FAIL — the setter tests fail because no exception is thrown (auto-property today); the `int.MaxValue` test fails with an `OverflowException` from `checked(...)`; the `$schema` test fails because no error is reported.

- [ ] **Step 3: Implement**

Replace the body of `src/Motiv.Serialization/RuleSerializerOptions.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>Options that control how rule documents are validated and loaded.</summary>
public sealed class RuleSerializerOptions
{
    private int _maxDocumentDepth = 64;
    private int _maxNodeCount = 10_000;

    /// <summary>The maximum nesting depth a rule document may have. Defaults to 64.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxDocumentDepth
    {
        get => _maxDocumentDepth;
        set => _maxDocumentDepth = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "MaxDocumentDepth must be at least 1.");
    }

    /// <summary>The maximum number of rule nodes a document may contain. Defaults to 10,000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxNodeCount
    {
        get => _maxNodeCount;
        set => _maxNodeCount = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "MaxNodeCount must be at least 1.");
    }
}
```

In `src/Motiv.Serialization/RuleDocumentParser.cs`, replace the reader-options line (currently `checked(options.MaxDocumentDepth * 2 + 4)`):

```csharp
            // Binary-operator nesting costs 2 JSON levels per rule level, so the reader's depth
            // ceiling must be raised beyond STJ's default of 64 to admit any document that is
            // legal under MaxDocumentDepth. Clamped so extreme option values cannot overflow.
            var maxDepth = (int)Math.Min((long)options.MaxDocumentDepth * 2 + 4, int.MaxValue);
            var readerOptions = new JsonDocumentOptions { MaxDepth = maxDepth };
```

And replace the `$schema` case in `ParseEnvelope`:

```csharp
                case "$schema":
                    if (property.Value.ValueKind != JsonValueKind.String)
                        errors.Add(new RuleError("$.$schema", RuleErrorCode.InvalidNode,
                            "'$schema' must be a string"));
                    break;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS (all serialization tests).

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization/RuleSerializerOptions.cs src/Motiv.Serialization/RuleDocumentParser.cs src/Motiv.Serialization.Tests/RuleSerializerOptionsTests.cs src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs
git commit -m "fix: validate serializer option ranges and clamp the reader depth formula"
```

---

### Task 2: Parameter declaration parsing

**Files:**
- Create: `src/Motiv.Serialization/RuleParameterType.cs`
- Create: `src/Motiv.Serialization/RuleParameterDeclaration.cs`
- Modify: `src/Motiv.Serialization/RuleDocument.cs`
- Modify: `src/Motiv.Serialization/RuleDocumentParser.cs` (replace `ValidateParameterDeclarations`)
- Create: `src/Motiv.Serialization.Tests/RuleParameterTests.cs`

**Interfaces:**
- Consumes: `RuleDocumentParser.ParseEnvelope`, `RuleError`, `RuleErrorCode.InvalidNode`.
- Produces (internal, used by Tasks 3–4): `RuleParameterType { Integer, Number, String, Boolean }`; `RuleParameterDeclaration` with `string Name`, `RuleParameterType Type`, `bool HasDefault`, `object? DefaultValue` (an `int`, `double`, `string`, or `bool` when present); `RuleDocument.Parameters` of type `IReadOnlyList<RuleParameterDeclaration>`.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/RuleParameterTests.cs`:

```csharp
namespace Motiv.Serialization.Tests;

public class RuleParameterTests
{
    private static IReadOnlyList<RuleError> Validate(string json) =>
        new RuleSerializer(new SpecRegistry()).Validate(json);

    [Theory]
    [InlineData("""{ "parameters": { "p": { "type": "integer", "default": "x" } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "integer", "default": 2.5 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "number", "default": true } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "string", "default": 1 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "boolean", "default": "yes" } }, "rule": { "spec": "a" } }""")]
    public void Should_report_defaults_that_do_not_match_the_declared_type(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p.default");
    }

    [Fact]
    public void Should_report_an_integer_default_outside_32_bit_range()
    {
        // Act
        var errors = Validate(
            """{ "parameters": { "p": { "type": "integer", "default": 2147483648 } }, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p.default");
    }

    [Theory]
    [InlineData("""{ "parameters": { "p": { "type": "integer", "default": 18 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "number", "default": 1.5 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "string", "default": "x" } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "boolean", "default": false } }, "rule": { "spec": "a" } }""")]
    public void Should_accept_defaults_that_match_the_declared_type(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleParameterTests"`
Expected: the mismatch/range tests FAIL (no errors reported today — the parser ignores `default`); the accept tests pass.

- [ ] **Step 3: Implement declaration parsing**

Create `src/Motiv.Serialization/RuleParameterType.cs`:

```csharp
namespace Motiv.Serialization;

internal enum RuleParameterType
{
    Integer,
    Number,
    String,
    Boolean
}
```

Create `src/Motiv.Serialization/RuleParameterDeclaration.cs`:

```csharp
namespace Motiv.Serialization;

internal sealed class RuleParameterDeclaration(
    string name,
    RuleParameterType type,
    bool hasDefault,
    object? defaultValue)
{
    public string Name { get; } = name;

    public RuleParameterType Type { get; } = type;

    public bool HasDefault { get; } = hasDefault;

    public object? DefaultValue { get; } = defaultValue;
}
```

Replace `src/Motiv.Serialization/RuleDocument.cs`:

```csharp
namespace Motiv.Serialization;

internal sealed class RuleDocument(
    string? name,
    RuleNode? root,
    IReadOnlyList<RuleParameterDeclaration> parameters)
{
    public string? Name { get; } = name;

    public RuleNode? Root { get; } = root;

    public IReadOnlyList<RuleParameterDeclaration> Parameters { get; } = parameters;
}
```

In `src/Motiv.Serialization/RuleDocumentParser.cs`:

1. In `ParseEnvelope`, declare `var parameters = new List<RuleParameterDeclaration>();` before the loop, change the `"parameters"` case to `parameters = ParseParameterDeclarations(property.Value, errors);`, and end with `return new RuleDocument(name, rule, parameters);`.
2. Replace `ValidateParameterDeclarations` with:

```csharp
    private static List<RuleParameterDeclaration> ParseParameterDeclarations(
        JsonElement element,
        List<RuleError> errors)
    {
        var declarations = new List<RuleParameterDeclaration>();
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError("$.parameters", RuleErrorCode.InvalidNode,
                "'parameters' must be a JSON object"));
            return declarations;
        }

        foreach (var parameter in element.EnumerateObject())
        {
            var declaration = ParseParameterDeclaration(parameter, errors);
            if (declaration is not null)
                declarations.Add(declaration);
        }

        return declarations;
    }

    private static RuleParameterDeclaration? ParseParameterDeclaration(
        JsonProperty parameter,
        List<RuleError> errors)
    {
        var path = $"$.parameters.{parameter.Name}";
        if (parameter.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "parameter declaration must be a JSON object"));
            return null;
        }

        string? typeName = null;
        JsonElement? defaultElement = null;
        foreach (var property in parameter.Value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "type":
                    typeName = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : null;
                    break;
                case "default":
                    defaultElement = property.Value;
                    break;
                default:
                    errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                        $"unknown property '{property.Name}'"));
                    break;
            }
        }

        RuleParameterType? type = typeName switch
        {
            "integer" => RuleParameterType.Integer,
            "number" => RuleParameterType.Number,
            "string" => RuleParameterType.String,
            "boolean" => RuleParameterType.Boolean,
            _ => null
        };
        if (type is null)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "parameter declaration must declare a 'type' of 'integer', 'number', 'string' or 'boolean'"));
            return null;
        }

        if (defaultElement is null)
            return new RuleParameterDeclaration(parameter.Name, type.Value, hasDefault: false, defaultValue: null);

        var defaultValue = ParseDefault(type.Value, defaultElement.Value, $"{path}.default", errors);
        return defaultValue is null
            ? null
            : new RuleParameterDeclaration(parameter.Name, type.Value, hasDefault: true, defaultValue);
    }

    private static object? ParseDefault(
        RuleParameterType type,
        JsonElement element,
        string path,
        List<RuleError> errors)
    {
        switch (type)
        {
            case RuleParameterType.Integer when element.ValueKind == JsonValueKind.Number:
                if (element.TryGetInt32(out var integer))
                    return integer;
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "integer parameter default must fit in a 32-bit integer"));
                return null;
            case RuleParameterType.Number when element.ValueKind == JsonValueKind.Number:
                return element.GetDouble();
            case RuleParameterType.String when element.ValueKind == JsonValueKind.String:
                return element.GetString();
            case RuleParameterType.Boolean when element.ValueKind is JsonValueKind.True or JsonValueKind.False:
                return element.GetBoolean();
            default:
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    $"parameter default must match the declared type '{type.ToString().ToLowerInvariant()}'"));
                return null;
        }
    }
```

3. Delete the now-unused `ParameterTypes` static field.

Note: the two `new RuleDocument(...)` construction sites (`ParseEnvelope` return and any early returns) must all pass the parameters list — pass `[]` where declarations were never parsed. There is one early `return null` for a non-object root; leave that as-is.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS — including the pre-existing declaration tests in `RuleSerializerValidateTests` (missing `type`, bad `type`, unknown property), whose messages are unchanged except the missing/bad-`type` message which now reads "parameter declaration must declare a 'type' of 'integer', 'number', 'string' or 'boolean'". If a Plan-1 test asserts the old message text "parameter declaration must declare a 'type'" with `ShouldContain`, it still passes (prefix preserved); if it asserts the old "parameter type must be one of ..." message emitted at `$.parameters.p.type`, update that test to expect the new single error at `$.parameters.p`.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: parse parameter declarations with type-checked defaults"
```

---

### Task 3: Parameter resolution + `Deserialize` parameter overloads

**Files:**
- Create: `src/Motiv.Serialization/RuleParameterResolver.cs`
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs` (append `MissingParameter`, `SurplusParameter`, `ParameterTypeMismatch`)
- Modify: `src/Motiv.Serialization/RuleSerializer.cs`
- Modify: `src/Motiv.Serialization.Tests/RuleParameterTests.cs`

**Interfaces:**
- Consumes: `RuleParameterDeclaration` (Task 2), `RuleDocument.Parameters`, `RuleBinder.Bind<TModel>`.
- Produces:
  - Public: `SpecBase<TModel, string> Deserialize<TModel>(string json, object? parameters)` and `SpecBase<TModel, string> Deserialize<TModel>(string json, IReadOnlyDictionary<string, object?>? parameters)`.
  - Internal (used by Tasks 4, 9, 10): `RuleParameterResolver.ToDictionary(object?)`, `RuleParameterResolver.Resolve(declarations, supplied, errors)` returning `Dictionary<string, object?>` (values are `int`/`double`/`string`/`bool`; placeholders fill erroneous slots so later stages don't cascade), and `RuleParameterResolver.ResolveForValidation(declarations)`.
  - `RuleSerializer.Prepare(json, parameters, errors)` private helper — Task 4 extends it, Tasks 9–10 reuse it.

- [ ] **Step 1: Write the failing tests**

Add to `src/Motiv.Serialization.Tests/RuleParameterTests.cs` (top of class — shared fixtures the remaining tasks also use):

```csharp
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry().Register("is-positive", IsPositive));
```

And the tests:

```csharp
    [Fact]
    public void Should_throw_when_a_required_parameter_is_not_supplied()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MissingParameter);
        error.Path.ShouldBe("$.parameters.minOrders");
    }

    [Fact]
    public void Should_throw_when_a_surplus_parameter_is_supplied()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json, new { extra = 1 });

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.SurplusParameter);
        error.Path.ShouldBe("$.parameters.extra");
    }

    [Fact]
    public void Should_throw_when_a_supplied_parameter_does_not_match_the_declared_type()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json, new { minOrders = "three" });

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.parameters.minOrders");
    }

    [Fact]
    public void Should_accept_required_parameters_from_an_anonymous_object()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, new { minOrders = 3 });

        // Assert
        loaded.ShouldNotBeNull();
    }

    [Fact]
    public void Should_accept_required_parameters_from_a_dictionary()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";
        var parameters = new Dictionary<string, object?> { ["minOrders"] = 3L };

        // Act — a long that fits in 32 bits coerces to an integer parameter
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        loaded.ShouldNotBeNull();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleParameterTests"`
Expected: FAIL — the new codes and overloads do not exist yet (compile errors are the expected failure mode here; fix by implementing, not by changing the tests).

- [ ] **Step 3: Implement**

Append to the enum in `src/Motiv.Serialization/RuleErrorCode.cs` (after `DocumentTooLarge` — append-only contract):

```csharp
    /// <summary>A required parameter was not supplied and has no default.</summary>
    MissingParameter,

    /// <summary>A supplied parameter is not declared by the document.</summary>
    SurplusParameter,

    /// <summary>A supplied parameter value does not match the declared parameter type.</summary>
    ParameterTypeMismatch
```

Create `src/Motiv.Serialization/RuleParameterResolver.cs`:

```csharp
using System.Reflection;

namespace Motiv.Serialization;

internal static class RuleParameterResolver
{
    public static IReadOnlyDictionary<string, object?>? ToDictionary(object? parameters) =>
        parameters switch
        {
            null => null,
            IReadOnlyDictionary<string, object?> dictionary => dictionary,
            _ => parameters.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(property => property.Name, property => property.GetValue(parameters))
        };

    public static Dictionary<string, object?> Resolve(
        IReadOnlyList<RuleParameterDeclaration> declarations,
        IReadOnlyDictionary<string, object?>? supplied,
        List<RuleError> errors)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
            values[declaration.Name] = ResolveValue(declaration, supplied, errors);

        if (supplied is null)
            return values;

        var surplus = supplied.Keys.Where(name => declarations.All(declaration => declaration.Name != name));
        foreach (var name in surplus)
            errors.Add(new RuleError($"$.parameters.{name}", RuleErrorCode.SurplusParameter,
                $"no parameter named '{name}' is declared by the document"));

        return values;
    }

    public static Dictionary<string, object?> ResolveForValidation(
        IReadOnlyList<RuleParameterDeclaration> declarations) =>
        declarations.ToDictionary(
            declaration => declaration.Name,
            declaration => declaration.HasDefault ? declaration.DefaultValue : Placeholder(declaration),
            StringComparer.Ordinal);

    private static object? ResolveValue(
        RuleParameterDeclaration declaration,
        IReadOnlyDictionary<string, object?>? supplied,
        List<RuleError> errors)
    {
        if (supplied is not null && supplied.TryGetValue(declaration.Name, out var value))
            // A placeholder stands in on mismatch so interpolation does not cascade errors.
            return Coerce(declaration, value, errors) ?? Placeholder(declaration);

        if (declaration.HasDefault)
            return declaration.DefaultValue;

        errors.Add(new RuleError($"$.parameters.{declaration.Name}", RuleErrorCode.MissingParameter,
            $"the required parameter '{declaration.Name}' was not supplied"));
        return Placeholder(declaration);
    }

    private static object? Coerce(
        RuleParameterDeclaration declaration,
        object? value,
        List<RuleError> errors)
    {
        object? coerced = (declaration.Type, value) switch
        {
            (RuleParameterType.Integer, int integer) => integer,
            (RuleParameterType.Integer, long l) when l is >= int.MinValue and <= int.MaxValue => (int)l,
            (RuleParameterType.Number, double d) => d,
            (RuleParameterType.Number, float f) => (double)f,
            (RuleParameterType.Number, int integer) => (double)integer,
            (RuleParameterType.Number, long l) => (double)l,
            (RuleParameterType.Number, decimal m) => (double)m,
            (RuleParameterType.String, string s) => s,
            (RuleParameterType.Boolean, bool b) => b,
            _ => null
        };

        if (coerced is null)
            errors.Add(new RuleError($"$.parameters.{declaration.Name}", RuleErrorCode.ParameterTypeMismatch,
                $"the supplied value for '{declaration.Name}' does not match the declared type " +
                $"'{declaration.Type.ToString().ToLowerInvariant()}'"));

        return coerced;
    }

    private static object Placeholder(RuleParameterDeclaration declaration) =>
        declaration.Type switch
        {
            RuleParameterType.Integer => 0,
            RuleParameterType.Number => 0d,
            RuleParameterType.Boolean => false,
            _ => declaration.Name
        };
}
```

In `src/Motiv.Serialization/RuleSerializer.cs`, replace `Deserialize<TModel>` with three overloads plus a shared `Prepare` helper (keep the existing XML docs on the first overload and add equivalents to the new ones):

```csharp
    public SpecBase<TModel, string> Deserialize<TModel>(string json) =>
        Deserialize<TModel>(json, (IReadOnlyDictionary<string, object?>?)null);

    public SpecBase<TModel, string> Deserialize<TModel>(string json, object? parameters) =>
        Deserialize<TModel>(json, RuleParameterResolver.ToDictionary(parameters));

    public SpecBase<TModel, string> Deserialize<TModel>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = RuleBinder.Bind<TModel>(document!, _registry, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    private RuleDocument? Prepare(
        string json,
        IReadOnlyDictionary<string, object?>? parameters,
        List<RuleError> errors)
    {
        var document = new RuleDocumentParser(_options).Parse(json, errors);
        if (document is null)
            return null;

        var values = RuleParameterResolver.Resolve(document.Parameters, parameters, errors);
        _ = values; // Task 4 replaces this discard with RuleParameterSubstituter.Apply.
        return document;
    }
```

XML docs for the two new overloads: same summary as the parameterless one plus a `<param name="parameters">` line — anonymous-object overload: "An object whose public properties supply parameter values, or <c>null</c>."; dictionary overload: "Parameter values keyed by declared parameter name, or <c>null</c>."

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: resolve supplied rule parameters against document declarations"
```

---

### Task 4: `{param}` interpolation in payload strings

**Files:**
- Create: `src/Motiv.Serialization/RuleParameterSubstituter.cs`
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs` (append `UnknownParameterReference`)
- Modify: `src/Motiv.Serialization/RuleSerializer.cs` (wire the substituter into `Prepare`)
- Modify: `src/Motiv.Serialization.Tests/RuleParameterTests.cs`

**Interfaces:**
- Consumes: `RuleNode.WhenTrueText`/`WhenFalseText`/`Children`/`Path`, resolved values from Task 3.
- Produces (internal, extended in Task 6 with `n` resolution): `RuleParameterSubstituter.Apply(RuleNode node, IReadOnlyDictionary<string, object?> values, List<RuleError> errors)` — mutates payload strings in place.

- [ ] **Step 1: Write the failing tests**

Add to `src/Motiv.Serialization.Tests/RuleParameterTests.cs` (add `using static Motiv.Serialization.Tests.SpecAssertions;` to the file):

```csharp
    [Fact]
    public void Should_interpolate_default_parameter_values_into_payload_strings()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer", "default": 18 } },
              "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "under {minAge}" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("at least 18")
            .WhenFalse("under 18")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_interpolate_supplied_values_over_defaults()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer", "default": 18 } },
              "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "under {minAge}" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("at least 21")
            .WhenFalse("under 21")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, new { minAge = 21 });

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_format_number_and_boolean_parameters_invariantly()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": {
                "rate": { "type": "number", "default": 1.5 },
                "strict": { "type": "boolean", "default": true }
              },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate} strict {strict}", "whenFalse": "no" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("rate 1.5 strict true")
            .WhenFalse("no")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_unescape_doubled_braces_as_literals()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer", "default": 18 } },
              "rule": { "spec": "is-positive", "whenTrue": "{{age}} >= {minAge}", "whenFalse": "no" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("{age} >= 18")
            .WhenFalse("no")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_report_an_unknown_parameter_reference_in_a_payload_string()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "no" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownParameterReference);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "is-positive", "whenTrue": "broken {", "whenFalse": "no" } }""")]
    [InlineData("""{ "rule": { "spec": "is-positive", "whenTrue": "broken }", "whenFalse": "no" } }""")]
    public void Should_report_unmatched_braces_in_a_payload_string(string json)
    {
        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Fact]
    public void Should_report_a_payload_that_interpolates_to_whitespace()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "label": { "type": "string", "default": " " } },
              "rule": { "spec": "is-positive", "whenTrue": "{label}", "whenFalse": "no" }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.whenTrue");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleParameterTests"`
Expected: FAIL — interpolation tests load specs whose assertions still contain the literal `{minAge}` text; the error tests fail to compile until the enum member exists.

- [ ] **Step 3: Implement**

Append to `src/Motiv.Serialization/RuleErrorCode.cs` (after `ParameterTypeMismatch`):

```csharp
    /// <summary>A payload string or 'n' slot references a parameter that is not declared.</summary>
    UnknownParameterReference
```

Create `src/Motiv.Serialization/RuleParameterSubstituter.cs`:

```csharp
using System.Globalization;
using System.Text;

namespace Motiv.Serialization;

internal static class RuleParameterSubstituter
{
    public static void Apply(
        RuleNode node,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        node.WhenTrueText = Interpolate(node.WhenTrueText, $"{node.Path}.whenTrue", values, errors);
        node.WhenFalseText = Interpolate(node.WhenFalseText, $"{node.Path}.whenFalse", values, errors);

        foreach (var child in node.Children)
            Apply(child, values, errors);
    }

    private static string? Interpolate(
        string? text,
        string path,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        if (text is null || (text.IndexOf('{') < 0 && text.IndexOf('}') < 0))
            return text;

        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{' when i + 1 < text.Length && text[i + 1] == '{':
                    result.Append('{');
                    i++;
                    break;
                case '}' when i + 1 < text.Length && text[i + 1] == '}':
                    result.Append('}');
                    i++;
                    break;
                case '}':
                    errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                        "unmatched '}' in payload string; use '}}' to escape a literal brace"));
                    return null;
                case '{':
                    var end = text.IndexOf('}', i + 1);
                    if (end < 0)
                    {
                        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                            "unmatched '{' in payload string; use '{{' to escape a literal brace"));
                        return null;
                    }

                    var name = text.Substring(i + 1, end - i - 1);
                    if (!values.TryGetValue(name, out var value))
                    {
                        errors.Add(new RuleError(path, RuleErrorCode.UnknownParameterReference,
                            $"unknown parameter '{name}'"));
                        return null;
                    }

                    result.Append(Format(value));
                    i = end;
                    break;
                default:
                    result.Append(text[i]);
                    break;
            }
        }

        var interpolated = result.ToString();
        if (!string.IsNullOrWhiteSpace(interpolated))
            return interpolated;

        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
            "payload string must not be empty or whitespace after parameter interpolation"));
        return null;
    }

    private static string Format(object? value) =>
        value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? string.Empty
        };
}
```

In `RuleSerializer.Prepare`, replace the `_ = values;` discard line with:

```csharp
        if (document.Root is not null)
            RuleParameterSubstituter.Apply(document.Root, values, errors);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: interpolate load-time parameters into payload strings"
```

---

### Task 5: Higher-order node parsing

**Files:**
- Modify: `src/Motiv.Serialization/RuleOperator.cs` (five new members + helper extensions)
- Modify: `src/Motiv.Serialization/RuleNode.cs` (`N`, `NParameterName`, `PathText`)
- Modify: `src/Motiv.Serialization/RuleDocumentParser.cs`
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (explicit not-yet-bound guard, replaced in Task 6)
- Modify: `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`

**Interfaces:**
- Consumes: existing parser plumbing (`ParseNode`, `ParseOperator`, `ReadNonEmptyString`).
- Produces (used by Tasks 6–7, 9):
  - `RuleOperator.AsAllSatisfied`, `AsAnySatisfied`, `AsNSatisfied`, `AsAtLeastNSatisfied`, `AsAtMostNSatisfied` (appended in this order).
  - `RuleOperatorExtensions.IsHigherOrder(this RuleOperator)` and `RuleOperatorExtensions.RequiresN(this RuleOperator)`.
  - `RuleNode.N` (`int?`), `RuleNode.NParameterName` (`string?`, the reference without its `@`), `RuleNode.PathText` (`string?`). A higher-order node's inner node is `Children[0]` and its JSON path is `{node.Path}.{operatorName}`.
  - Parse guarantees the binder can rely on: a higher-order node always has `name` or string/object payloads; N-forms have `N` or `NParameterName`; `n`/`path` never appear on non-higher-order nodes.

- [ ] **Step 1: Write the failing tests**

In `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`, **delete** `Should_explain_that_higher_order_properties_are_not_yet_supported` and add:

```csharp
    [Theory]
    [InlineData("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "name": "all" } }""")]
    [InlineData("""{ "rule": { "asAnySatisfied": { "spec": "a" }, "whenTrue": "some", "whenFalse": "none" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 3, "name": "exactly three" } }""")]
    [InlineData("""{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": "@minOrders", "name": "quota" } }""")]
    [InlineData("""{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 0, "path": "Orders", "name": "none" } }""")]
    public void Should_accept_well_formed_higher_order_nodes(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_require_n_on_counted_higher_order_nodes()
    {
        // Act
        var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("'n'");
    }

    [Fact]
    public void Should_reject_n_on_uncounted_higher_order_nodes()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": 3, "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
    }

    [Theory]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": -1, "name": "x" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 2.5, "name": "x" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "minOrders", "name": "x" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "@1bad", "name": "x" } }""")]
    public void Should_reject_malformed_n_values(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "a", "n": 3 } }""", "$.rule.n")]
    [InlineData("""{ "rule": { "spec": "a", "path": "Orders" } }""", "$.rule.path")]
    public void Should_reject_higher_order_properties_on_other_nodes(string json, string path)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(path);
    }

    [Fact]
    public void Should_require_a_name_or_payloads_on_higher_order_nodes()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("name");
    }

    [Fact]
    public void Should_reject_an_empty_higher_order_path()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "", "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.path");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSerializerValidateTests"`
Expected: FAIL — every higher-order document currently produces the Plan-1 "not yet supported" `InvalidNode` errors, so the accept tests see non-empty error lists and the error tests see the wrong paths/messages.

- [ ] **Step 3: Implement**

Replace `src/Motiv.Serialization/RuleOperator.cs`:

```csharp
namespace Motiv.Serialization;

internal enum RuleOperator
{
    Spec,
    Expression,
    And,
    Or,
    XOr,
    AndAlso,
    OrElse,
    Not,
    AsAllSatisfied,
    AsAnySatisfied,
    AsNSatisfied,
    AsAtLeastNSatisfied,
    AsAtMostNSatisfied
}

internal static class RuleOperatorExtensions
{
    public static bool IsHigherOrder(this RuleOperator @operator) =>
        @operator is RuleOperator.AsAllSatisfied
            or RuleOperator.AsAnySatisfied
            or RuleOperator.AsNSatisfied
            or RuleOperator.AsAtLeastNSatisfied
            or RuleOperator.AsAtMostNSatisfied;

    public static bool RequiresN(this RuleOperator @operator) =>
        @operator is RuleOperator.AsNSatisfied
            or RuleOperator.AsAtLeastNSatisfied
            or RuleOperator.AsAtMostNSatisfied;
}
```

Add to `src/Motiv.Serialization/RuleNode.cs` (after `Name`):

```csharp
    public int? N { get; set; }

    public string? NParameterName { get; set; }

    public string? PathText { get; set; }
```

In `src/Motiv.Serialization/RuleDocumentParser.cs`:

1. Delete the `HigherOrderProperties` field and the special-cased "not yet supported" message in `ParseNode`'s `default` case (keep the plain `unknown property` error).
2. In `ParseNode`, declare `JsonElement? nElement = null;` and `JsonElement? pathElement = null;` beside `whenTrue`/`whenFalse`, and extend the property switch:

```csharp
                case "spec" or "expression" or "not" or "and" or "or" or "xor" or "andAlso" or "orElse"
                    or "asAllSatisfied" or "asAnySatisfied" or "asNSatisfied"
                    or "asAtLeastNSatisfied" or "asAtMostNSatisfied":
                    operators.Add(property);
                    break;
                case "n":
                    nElement = property.Value;
                    break;
                case "path":
                    pathElement = property.Value;
                    break;
```

3. Update the `operators.Count != 1` message to name the full vocabulary:

```csharp
                "rule node must contain exactly one of 'spec', 'expression', 'not', 'and', 'or', 'xor', " +
                "'andAlso', 'orElse', 'asAllSatisfied', 'asAnySatisfied', 'asNSatisfied', " +
                "'asAtLeastNSatisfied' or 'asAtMostNSatisfied'"
```

(If a Plan-1 test asserts the old shorter message verbatim, update it to the new text.)

4. Also in the `operators.Count != 1` early-exit branch, `n`/`path` are silently dropped — the node error already covers the document. In the success path, after `ParsePayloads(...)` and the `node is null` check, add:

```csharp
        ApplyHigherOrderProperties(node, nElement, pathElement, path, errors);
        node.Name = name;

        if (node.Operator.IsHigherOrder() && node.Name is null
            && node.WhenTrueText is null && !node.HasObjectPayloads)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "higher-order nodes must declare a 'name' or 'whenTrue'/'whenFalse' payloads"));
            return null;
        }

        return node;
```

5. Add the higher-order case to `ParseOperator` (before the `default` arm):

```csharp
            case "asAllSatisfied" or "asAnySatisfied" or "asNSatisfied"
                or "asAtLeastNSatisfied" or "asAtMostNSatisfied":
            {
                var @operator = property.Name switch
                {
                    "asAllSatisfied" => RuleOperator.AsAllSatisfied,
                    "asAnySatisfied" => RuleOperator.AsAnySatisfied,
                    "asNSatisfied" => RuleOperator.AsNSatisfied,
                    "asAtLeastNSatisfied" => RuleOperator.AsAtLeastNSatisfied,
                    _ => RuleOperator.AsAtMostNSatisfied
                };

                var child = ParseNode(property.Value, $"{path}.{property.Name}", depth + 1, errors);
                if (child is null)
                    return null;

                var node = new RuleNode(@operator, path);
                node.Children.Add(child);
                return node;
            }
```

6. Add the new private helpers:

```csharp
    private static void ApplyHigherOrderProperties(
        RuleNode node,
        JsonElement? nElement,
        JsonElement? pathElement,
        string path,
        List<RuleError> errors)
    {
        if (nElement is { } n)
        {
            if (node.Operator.RequiresN())
                ParseN(n, node, $"{path}.n", errors);
            else
                errors.Add(new RuleError($"{path}.n", RuleErrorCode.InvalidNode,
                    "'n' is only valid on 'asNSatisfied', 'asAtLeastNSatisfied' and 'asAtMostNSatisfied' nodes"));
        }
        else if (node.Operator.RequiresN())
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "this node requires 'n' (a non-negative integer or a '@parameter' reference)"));
        }

        if (pathElement is { } pathValue)
        {
            if (node.Operator.IsHigherOrder())
                node.PathText = ReadNonEmptyString(pathValue, $"{path}.path", errors);
            else
                errors.Add(new RuleError($"{path}.path", RuleErrorCode.InvalidNode,
                    "'path' is only valid on higher-order nodes"));
        }
    }

    private static void ParseN(JsonElement element, RuleNode node, string path, List<RuleError> errors)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetInt32(out var n) && n >= 0:
                node.N = n;
                return;
            case JsonValueKind.String when IsParameterReference(element.GetString()):
                node.NParameterName = element.GetString()!.Substring(1);
                return;
            default:
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "'n' must be a non-negative integer or a '@parameter' reference"));
                return;
        }
    }

    private static bool IsParameterReference(string? text)
    {
        if (text is null || text.Length < 2 || text[0] != '@')
            return false;

        return IsIdentifierStart(text[1]) && text.Skip(2).All(IsIdentifierPart);

        static bool IsIdentifierStart(char ch) => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
        static bool IsIdentifierPart(char ch) => IsIdentifierStart(ch) || ch is >= '0' and <= '9';
    }
```

7. In `src/Motiv.Serialization/RuleBinder.cs`, add an explicit guard arm to the `BindNode` switch so higher-order nodes can never silently fall into `BindComposition` (Task 6 replaces this arm with the real binding):

```csharp
            _ when node.Operator.IsHigherOrder() =>
                throw new NotSupportedException("higher-order binding lands in the next task"),
```

Place it directly above the existing `_ => BindComposition<TModel>(node, registry, errors)` arm.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: parse higher-order rule nodes with n and path properties"
```

---

### Task 6: Higher-order binding over collection models

**Files:**
- Create: `src/Motiv.Serialization/HigherOrderModelResolution.cs`
- Create: `src/Motiv.Serialization/HigherOrderModelResolver.cs`
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs` (append `InvalidHigherOrderPath`)
- Modify: `src/Motiv.Serialization/RuleParameterSubstituter.cs` (`n` resolution)
- Modify: `src/Motiv.Serialization/RuleBinder.cs`
- Create: `src/Motiv.Serialization.Tests/RuleHigherOrderTests.cs`

**Interfaces:**
- Consumes: Task 5 parse guarantees; `Spec.Build(spec).AsAllSatisfied()/.AsAnySatisfied()/.AsNSatisfied(n)/.AsAtLeastNSatisfied(n)/.AsAtMostNSatisfied(n)` fluent builders; `SpecBase<TModel, TMetadata>.ChangeModelTo<TNewModel>(Func<TNewModel, TModel>)`.
- Produces (Task 7 extends the resolver; Task 9 reuses both):
  - `HigherOrderModelResolution` with `Type ElementType`, `PropertyInfo[] Properties`, `object GetCollection(object model)`.
  - `HigherOrderModelResolver.Resolve(Type modelType, RuleNode node, List<RuleError> errors)` → `HigherOrderModelResolution?` (Task 6 handles only the no-`path` case).
  - `RuleBinder.BindHigherOrderCore<TModel, TElement>` invoked via `MakeGenericMethod`.
  - Substituter guarantee: after `Apply`, every N-form node has `N` populated or an error was reported.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/RuleHigherOrderTests.cs`:

```csharp
using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class RuleHigherOrderTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry().Register("is-positive", IsPositive));

    [Fact]
    public void Should_load_a_named_all_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "name": "all positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .Create("all positive");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_load_a_decorated_unnamed_any_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "whenTrue": "some are positive",
                "whenFalse": "none are positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAnySatisfied()
            .WhenTrue("some are positive")
            .WhenFalse("none are positive")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_load_a_decorated_named_higher_order_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAllSatisfied": { "spec": "is-positive" },
                "whenTrue": "all positive",
                "whenFalse": "some are not positive",
                "name": "positivity"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("some are not positive")
            .Create("positivity");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 });
    }

    [Fact]
    public void Should_load_an_exactly_n_satisfied_node_with_a_literal_n()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": 2, "name": "exactly two" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsNSatisfied(2)
            .Create("exactly two");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { 1, 2, 3 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_resolve_n_from_a_parameter_reference()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minOrders": { "type": "integer" } },
              "rule": { "asAtLeastNSatisfied": { "spec": "is-positive" }, "n": "@minOrders", "name": "quota" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtLeastNSatisfied(2)
            .Create("quota");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json, new { minOrders = 2 });

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { 1, -2, -3 });
    }

    [Fact]
    public void Should_load_an_at_most_n_satisfied_node()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAtMostNSatisfied": { "spec": "is-positive" }, "n": 1, "name": "at most one" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAtMostNSatisfied(1)
            .Create("at most one");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { 1, 2 });
    }

    [Fact]
    public void Should_load_a_composed_inner_tree_inside_a_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAllSatisfied": { "and": [ { "spec": "is-positive" }, { "not": { "spec": "is-positive" } } ] },
                "name": "contradiction everywhere"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive.And(IsPositive.Not()))
            .AsAllSatisfied()
            .Create("contradiction everywhere");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { -1 });
    }

    [Fact]
    public void Should_reanchor_to_a_concrete_collection_model_type()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "name": "all positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .Create("all positive")
            .ChangeModelTo<int[]>(models => models);

        // Act
        var loaded = CreateSerializer().Deserialize<int[]>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 });
    }

    [Fact]
    public void Should_report_a_model_that_is_not_a_collection()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "name": "all positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidHigherOrderPath);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_report_an_unknown_parameter_reference_in_n()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": "@missing", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownParameterReference);
        error.Path.ShouldBe("$.rule.n");
    }

    [Fact]
    public void Should_report_a_non_integer_parameter_referenced_by_n()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "label": { "type": "string", "default": "x" } },
              "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": "@label", "name": "x" }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.rule.n");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleHigherOrderTests"`
Expected: FAIL — `InvalidHigherOrderPath` doesn't compile yet; once added, binding tests hit the Task-5 `NotSupportedException` guard.

- [ ] **Step 3: Implement**

Append to `src/Motiv.Serialization/RuleErrorCode.cs` (after `UnknownParameterReference`):

```csharp
    /// <summary>A higher-order node's model or 'path' does not resolve to a collection.</summary>
    InvalidHigherOrderPath
```

Create `src/Motiv.Serialization/HigherOrderModelResolution.cs`:

```csharp
using System.Reflection;

namespace Motiv.Serialization;

internal sealed class HigherOrderModelResolution(Type elementType, PropertyInfo[] properties)
{
    public Type ElementType { get; } = elementType;

    public PropertyInfo[] Properties { get; } = properties;

    public object GetCollection(object model)
    {
        var current = model;
        foreach (var property in Properties)
            current = property.GetValue(current)
                ?? throw new InvalidOperationException(
                    $"'{property.DeclaringType!.Name}.{property.Name}' returned null while selecting " +
                    "the higher-order collection");
        return current;
    }
}
```

Create `src/Motiv.Serialization/HigherOrderModelResolver.cs` (Task 7 adds `path` walking):

```csharp
namespace Motiv.Serialization;

internal static class HigherOrderModelResolver
{
    public static HigherOrderModelResolution? Resolve(Type modelType, RuleNode node, List<RuleError> errors)
    {
        var elementType = FindElementType(modelType);
        if (elementType is not null)
            return new HigherOrderModelResolution(elementType, []);

        errors.Add(new RuleError(node.Path, RuleErrorCode.InvalidHigherOrderPath,
            $"'{modelType.Name}' is not a collection; a higher-order node needs an IEnumerable<T> " +
            "model or a 'path' that selects one"));
        return null;
    }

    private static Type? FindElementType(Type type)
    {
        if (IsEnumerableInterface(type))
            return type.GetGenericArguments()[0];

        return type.GetInterfaces()
            .Where(IsEnumerableInterface)
            .Select(candidate => candidate.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static bool IsEnumerableInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
}
```

In `src/Motiv.Serialization/RuleParameterSubstituter.cs`, add a `ResolveN(node, values, errors);` call in `Apply` (before the child recursion) and the method:

```csharp
    private static void ResolveN(
        RuleNode node,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        if (node.NParameterName is not { } parameterName)
            return;

        if (!values.TryGetValue(parameterName, out var value))
        {
            errors.Add(new RuleError($"{node.Path}.n", RuleErrorCode.UnknownParameterReference,
                $"unknown parameter '{parameterName}'"));
            return;
        }

        if (value is int n and >= 0)
            node.N = n;
        else
            errors.Add(new RuleError($"{node.Path}.n", RuleErrorCode.ParameterTypeMismatch,
                $"'n' requires a non-negative 'integer' parameter; '{parameterName}' does not qualify"));
    }
```

In `src/Motiv.Serialization/RuleBinder.cs`:

1. Add `using System.Reflection;` and the cached method handle:

```csharp
    private static readonly MethodInfo BindHigherOrderCoreMethod = typeof(RuleBinder)
        .GetMethod(nameof(BindHigherOrderCore), BindingFlags.NonPublic | BindingFlags.Static)!;
```

2. Replace the Task-5 `NotSupportedException` arm with:

```csharp
            _ when node.Operator.IsHigherOrder() => BindHigherOrder<TModel>(node, registry, errors),
```

3. Higher-order nodes decorate through their own fluent builder, so the shared `Decorate` must not wrap them again — change `BindNode`'s return line to:

```csharp
        if (spec is null || hasObjectPayloadError)
            return null;

        return node.Operator.IsHigherOrder() ? spec : Decorate(node, spec);
```

4. Add the binding methods:

```csharp
    private static SpecBase<TModel, string>? BindHigherOrder<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var resolution = HigherOrderModelResolver.Resolve(typeof(TModel), node, errors);
        if (resolution is null)
            return null;

        return (SpecBase<TModel, string>?)BindHigherOrderCoreMethod
            .MakeGenericMethod(typeof(TModel), resolution.ElementType)
            .Invoke(null, [node, resolution, registry, errors]);
    }

    private static SpecBase<TModel, string>? BindHigherOrderCore<TModel, TElement>(
        RuleNode node,
        HigherOrderModelResolution resolution,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var inner = BindNode<TElement>(node.Children[0], registry, errors);
        if (inner is null)
            return null;

        return ReanchorToModel<TModel, TElement>(CreateHigherOrder(node, inner), resolution);
    }

    private static SpecBase<IEnumerable<TElement>, string> CreateHigherOrder<TElement>(
        RuleNode node,
        SpecBase<TElement, string> inner)
    {
        // Parse guarantees: string payloads arrive as a pair, bare nodes carry a name, and
        // N-forms have a resolved N by the time binding runs.
        if (node.WhenTrueText is { } whenTrue)
        {
            var whenFalse = node.WhenFalseText!;
            return (node.Operator, node.Name) switch
            {
                (RuleOperator.AsAllSatisfied, { } name) =>
                    Spec.Build(inner).AsAllSatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsAllSatisfied, _) =>
                    Spec.Build(inner).AsAllSatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsAnySatisfied, { } name) =>
                    Spec.Build(inner).AsAnySatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsAnySatisfied, _) =>
                    Spec.Build(inner).AsAnySatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsNSatisfied, { } name) =>
                    Spec.Build(inner).AsNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsNSatisfied, _) =>
                    Spec.Build(inner).AsNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsAtLeastNSatisfied, { } name) =>
                    Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                (RuleOperator.AsAtLeastNSatisfied, _) =>
                    Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(),
                (RuleOperator.AsAtMostNSatisfied, { } name) =>
                    Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(name),
                _ =>
                    Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create()
            };
        }

        return node.Operator switch
        {
            RuleOperator.AsAllSatisfied => Spec.Build(inner).AsAllSatisfied().Create(node.Name!),
            RuleOperator.AsAnySatisfied => Spec.Build(inner).AsAnySatisfied().Create(node.Name!),
            RuleOperator.AsNSatisfied => Spec.Build(inner).AsNSatisfied(node.N!.Value).Create(node.Name!),
            RuleOperator.AsAtLeastNSatisfied =>
                Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).Create(node.Name!),
            _ => Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).Create(node.Name!)
        };
    }

    private static SpecBase<TModel, string> ReanchorToModel<TModel, TElement>(
        SpecBase<IEnumerable<TElement>, string> higherOrder,
        HigherOrderModelResolution resolution)
    {
        if (resolution.Properties.Length > 0)
            return higherOrder.ChangeModelTo<TModel>(model =>
                (IEnumerable<TElement>)resolution.GetCollection(model!));

        return typeof(TModel) == typeof(IEnumerable<TElement>)
            ? (SpecBase<TModel, string>)(object)higherOrder
            : higherOrder.ChangeModelTo<TModel>(model => (IEnumerable<TElement>)(object)model!);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: bind higher-order rule nodes over collection models"
```

---

### Task 7: Higher-order `path` selection

**Files:**
- Modify: `src/Motiv.Serialization/HigherOrderModelResolver.cs`
- Modify: `src/Motiv.Serialization.Tests/RuleHigherOrderTests.cs`

**Interfaces:**
- Consumes: `RuleNode.PathText` (Task 5), `HigherOrderModelResolution` (Task 6 — already carries the property chain and `ReanchorToModel` already consumes it; only the resolver changes).
- Produces: `HigherOrderModelResolver.Resolve` now walks `path` property chains; `InvalidHigherOrderPath` errors are reported at `{node.Path}.path` when a `path` is present, at `{node.Path}` otherwise.

- [ ] **Step 1: Write the failing tests**

Add to `src/Motiv.Serialization.Tests/RuleHigherOrderTests.cs` (nested model classes at the bottom of the test class):

```csharp
    private class Customer
    {
        public List<int> Orders { get; set; } = [];

        public Account Account { get; set; } = new();

        public int Age { get; set; }
    }

    private class Account
    {
        public List<int> Orders { get; set; } = [];
    }
```

And the tests:

```csharp
    [Fact]
    public void Should_select_the_collection_through_a_single_segment_path()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "Orders", "name": "all orders positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .Create("all orders positive")
            .ChangeModelTo<Customer>(customer => customer.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Orders = [1, 2] },
            new Customer { Orders = [1, -2] });
    }

    [Fact]
    public void Should_select_the_collection_through_a_nested_path()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "path": "Account.Orders",
                "name": "an account order is positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAnySatisfied()
            .Create("an account order is positive")
            .ChangeModelTo<Customer>(customer => customer.Account.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Account = new Account { Orders = [-1, 2] } },
            new Customer { Account = new Account { Orders = [-1, -2] } });
    }

    [Fact]
    public void Should_report_a_path_segment_that_is_not_a_public_property()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "Nope", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidHigherOrderPath);
        error.Path.ShouldBe("$.rule.path");
        error.Message.ShouldContain("Nope");
    }

    [Fact]
    public void Should_report_a_path_that_selects_a_non_collection()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "Age", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidHigherOrderPath);
        error.Path.ShouldBe("$.rule.path");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleHigherOrderTests"`
Expected: FAIL — `path` is currently ignored by the resolver, so `Deserialize<Customer>` reports `Customer` is not a collection (`$.rule`, not `$.rule.path`).

- [ ] **Step 3: Implement**

Replace `Resolve` in `src/Motiv.Serialization/HigherOrderModelResolver.cs` (add `using System.Reflection;`):

```csharp
    public static HigherOrderModelResolution? Resolve(Type modelType, RuleNode node, List<RuleError> errors)
    {
        var properties = new List<PropertyInfo>();
        var current = modelType;

        if (node.PathText is { } pathText)
        {
            foreach (var segment in pathText.Split('.'))
            {
                var property = current.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                if (property is null)
                {
                    errors.Add(new RuleError($"{node.Path}.path", RuleErrorCode.InvalidHigherOrderPath,
                        $"'{current.Name}' has no public instance property '{segment}'"));
                    return null;
                }

                properties.Add(property);
                current = property.PropertyType;
            }
        }

        var elementType = FindElementType(current);
        if (elementType is not null)
            return new HigherOrderModelResolution(elementType, [.. properties]);

        errors.Add(node.PathText is null
            ? new RuleError(node.Path, RuleErrorCode.InvalidHigherOrderPath,
                $"'{current.Name}' is not a collection; a higher-order node needs an IEnumerable<T> " +
                "model or a 'path' that selects one")
            : new RuleError($"{node.Path}.path", RuleErrorCode.InvalidHigherOrderPath,
                $"'{node.PathText}' selects '{current.Name}', which is not a collection (IEnumerable<T>)"));
        return null;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: select higher-order collections through property paths"
```

---

### Task 8: Retain object payloads + require `name` alongside them

**Files:**
- Modify: `src/Motiv.Serialization/RuleNode.cs`
- Modify: `src/Motiv.Serialization/RuleDocumentParser.cs`
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (error message only)
- Modify: `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`
- Modify: `src/Motiv.Serialization.Tests/RuleSerializerDeserializeTests.cs` (existing object-payload tests gain a `name`)

**Interfaces:**
- Consumes: parser payload classification (Plan 1), `JsonElement.Clone()`.
- Produces (Task 9 consumes): `RuleNode.WhenTrueElement`/`WhenFalseElement` (`JsonElement?`, cloned so they outlive the parsed `JsonDocument`); `RuleNode.HasObjectPayloads` becomes a derived read-only property. Parse guarantee: a node with object payloads always has a `name`.

- [ ] **Step 1: Write the failing tests**

Add to `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`:

```csharp
    [Fact]
    public void Should_require_a_name_on_nodes_with_object_payloads()
    {
        // Act
        var errors = Validate(
            """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("name");
    }

    [Fact]
    public void Should_accept_named_nodes_with_object_payloads()
    {
        // Act
        var errors = Validate(
            """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 }, "name": "coded" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }
```

Then update any existing Plan-1 tests that use object payloads **without** a `name` (search the test project for `"whenTrue": {`): add `"name": "..."` to those documents so they keep exercising their original concern (e.g. the explanation-load `MetadataTypeMismatch` binder error) rather than the new parse error. Keep asserted paths/codes as-is unless a test asserted the old binder message text — the message changes in this task.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSerializerValidateTests"`
Expected: the new `Should_require_a_name...` test FAILS (no error reported today); the accept test passes.

- [ ] **Step 3: Implement**

In `src/Motiv.Serialization/RuleNode.cs`, add `using System.Text.Json;` and replace the `HasObjectPayloads` auto-property:

```csharp
    // Cloned out of the parsed JsonDocument so they survive its disposal; deserialized to the
    // caller's TMetadata during a metadata load.
    public JsonElement? WhenTrueElement { get; set; }

    public JsonElement? WhenFalseElement { get; set; }

    public bool HasObjectPayloads => WhenTrueElement is not null;
```

In `src/Motiv.Serialization/RuleDocumentParser.cs`:

1. In `ParsePayloads`, replace `node.HasObjectPayloads = true;` with:

```csharp
            node.WhenTrueElement = whenTrue.Value.Clone();
            node.WhenFalseElement = whenFalse.Value.Clone();
```

2. In `ParseNode`, directly after `node.Name = name;` (before the higher-order name rule), add:

```csharp
        if (node.HasObjectPayloads && node.Name is null)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "nodes with object 'whenTrue'/'whenFalse' payloads must also declare a 'name'"));
            return null;
        }
```

In `src/Motiv.Serialization/RuleBinder.cs`, update `ReportObjectPayloadError`'s message (code and mechanics unchanged):

```csharp
        errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
            "object 'whenTrue'/'whenFalse' payloads cannot be bound with explanation (string) semantics; " +
            "use a metadata load (Deserialize<TModel, TMetadata>)"));
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: retain object payloads and require a name beside them"
```

---

### Task 9: Metadata loads — `Deserialize<TModel, TMetadata>`

**Files:**
- Modify: `src/Motiv.Serialization/RuleSerializerOptions.cs` (`MetadataJsonOptions`)
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (split `BindNode`/`BindOperator`, share reanchoring)
- Modify: `src/Motiv.Serialization/HigherOrderModelResolver.cs` (generic `Reanchor` helper)
- Create: `src/Motiv.Serialization/MetadataRuleBinder.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs`
- Modify: `src/Motiv.Serialization.Tests/SpecAssertions.cs` (metadata overload)
- Create: `src/Motiv.Serialization.Tests/RuleMetadataTests.cs`

**Interfaces:**
- Consumes: `RuleNode.WhenTrueElement`/`WhenFalseElement` (Task 8), `HigherOrderModelResolver`/`HigherOrderModelResolution` (Tasks 6–7), `JsonElement.Deserialize<T>(JsonSerializerOptions?)`.
- Produces:
  - Public: `RuleSerializerOptions.MetadataJsonOptions` (`JsonSerializerOptions?`); `SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(string json)` plus `(string, object?)` and `(string, IReadOnlyDictionary<string, object?>?)` overloads. `TMetadata == string` delegates to the explanation load.
  - Internal (Task 10 consumes): `RuleBinder.BindNode<TModel>` and `RuleBinder.BindOperator<TModel>` become `public static` on the internal class; `MetadataRuleBinder<TMetadata>` with `Bind<TModel>(RuleDocument, List<RuleError>)`; `HigherOrderModelResolver.Reanchor<TModel, TElement, TMetadata>(higherOrder, resolution)`.
  - Test helper: `SpecAssertions.ShouldBehaveIdentically<TModel, TMetadata>(loaded, expected, params TModel[])` which also compares `Values`.

- [ ] **Step 1: Write the failing tests**

Add the metadata overload to `src/Motiv.Serialization.Tests/SpecAssertions.cs` (the existing `<TModel>` overload stays and still wins overload resolution for string specs):

```csharp
    /// <summary>
    /// Asserts that a loaded metadata spec evaluates identically to its expected fluent-built
    /// equivalent, additionally comparing the metadata values surfaced by the evaluation.
    /// </summary>
    public static void ShouldBehaveIdentically<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> loaded,
        SpecBase<TModel, TMetadata> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
            actualResult.Values.ShouldBe(expectedResult.Values);
        }
    }
```

Create `src/Motiv.Serialization.Tests/RuleMetadataTests.cs`:

```csharp
using System.Text.Json;
using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class RuleMetadataTests
{
    public class RuleOutcome
    {
        public string Code { get; set; } = "";

        public override bool Equals(object? obj) => obj is RuleOutcome other && other.Code == Code;

        public override int GetHashCode() => Code.GetHashCode();

        public override string ToString() => Code;
    }

    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, RuleOutcome> HasOutcome { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue(new RuleOutcome { Code = "POS" })
            .WhenFalse(new RuleOutcome { Code = "NEG" })
            .Create("has outcome");

    private static RuleSerializer CreateSerializer(RuleSerializerOptions? options = null) =>
        new(new SpecRegistry()
                .Register("is-positive", IsPositive)
                .Register("has-outcome", HasOutcome),
            options);

    [Fact]
    public void Should_load_a_typed_registry_leaf_with_its_metadata_intact()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "has-outcome" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, HasOutcome, 5, -5);
    }

    [Fact]
    public void Should_remetadatize_a_string_leaf_with_object_payloads()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "spec": "is-positive",
                "whenTrue": { "Code": "OK" },
                "whenFalse": { "Code": "BAD" },
                "name": "coded"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue(new RuleOutcome { Code = "OK" })
            .WhenFalse(new RuleOutcome { Code = "BAD" })
            .Create("coded");

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_honor_the_configured_metadata_json_options()
    {
        // Arrange — camelCase payload properties only bind through MetadataJsonOptions
        const string json =
            """
            {
              "rule": {
                "spec": "is-positive",
                "whenTrue": { "code": "OK" },
                "whenFalse": { "code": "BAD" },
                "name": "coded"
              }
            }
            """;
        var options = new RuleSerializerOptions
        {
            MetadataJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        };
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue(new RuleOutcome { Code = "OK" })
            .WhenFalse(new RuleOutcome { Code = "BAD" })
            .Create("coded");

        // Act
        var loaded = CreateSerializer(options).Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_compose_typed_and_remetadatized_nodes()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "and": [
                  { "spec": "has-outcome" },
                  { "spec": "is-positive",
                    "whenTrue": { "Code": "OK" },
                    "whenFalse": { "Code": "BAD" },
                    "name": "coded" }
                ]
              }
            }
            """;
        var expected = HasOutcome.And(
            Spec.Build(IsPositive)
                .WhenTrue(new RuleOutcome { Code = "OK" })
                .WhenFalse(new RuleOutcome { Code = "BAD" })
                .Create("coded"));

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_reject_an_undecorated_string_leaf_in_a_metadata_load()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_reject_string_payloads_in_a_metadata_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "has-outcome", "whenTrue": "yes", "whenFalse": "no" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_report_a_payload_that_cannot_deserialize_to_the_metadata_type()
    {
        // Arrange — Code is a string; an object payload for it fails STJ deserialization
        const string json =
            """
            {
              "rule": {
                "spec": "is-positive",
                "whenTrue": { "Code": { "nested": true } },
                "whenFalse": { "Code": "BAD" },
                "name": "coded"
              }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Fact]
    public void Should_load_a_bare_higher_order_node_over_typed_inner_metadata()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "has-outcome" }, "name": "all coded" } }""";
        var expected = Spec
            .Build(HasOutcome)
            .AsAllSatisfied()
            .Create("all coded");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 });
    }

    [Fact]
    public void Should_remetadatize_a_higher_order_node_with_object_payloads()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "whenTrue": { "Code": "SOME" },
                "whenFalse": { "Code": "NONE" },
                "name": "any positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAnySatisfied()
            .WhenTrue(new RuleOutcome { Code = "SOME" })
            .WhenFalse(new RuleOutcome { Code = "NONE" })
            .Create("any positive");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_reject_object_payloads_nested_inside_a_remetadatized_subtree()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "and": [
                  { "spec": "is-positive" },
                  { "spec": "is-positive",
                    "whenTrue": { "Code": "A" },
                    "whenFalse": { "Code": "B" },
                    "name": "inner" }
                ],
                "whenTrue": { "Code": "OUTER-T" },
                "whenFalse": { "Code": "OUTER-F" },
                "name": "outer"
              }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.ShouldContain(error =>
            error.Code == RuleErrorCode.MetadataTypeMismatch && error.Path == "$.rule.and[1]");
    }

    [Fact]
    public void Should_treat_a_string_metadata_load_as_an_explanation_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad" } }""";
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("ok")
            .WhenFalse("bad")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int, string>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleMetadataTests"`
Expected: FAIL to compile — `Deserialize<TModel, TMetadata>` and `MetadataJsonOptions` don't exist yet.

- [ ] **Step 3: Implement**

Add to `src/Motiv.Serialization/RuleSerializerOptions.cs` (`using System.Text.Json;` at the top):

```csharp
    /// <summary>
    /// The <see cref="JsonSerializerOptions" /> used to deserialize object 'whenTrue'/'whenFalse'
    /// payloads into the metadata type of a metadata load. <c>null</c> uses System.Text.Json defaults.
    /// </summary>
    public JsonSerializerOptions? MetadataJsonOptions { get; set; }
```

In `src/Motiv.Serialization/RuleBinder.cs`:

1. Make `BindNode<TModel>` `public static` and split the operator switch out of it:

```csharp
    public static SpecBase<TModel, string>? BindNode<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var spec = BindOperator<TModel>(node, registry, errors);

        // Reported independently of leaf/composition success, mirroring the parser's approach
        // of surfacing payload errors even when the operator subtree fails.
        var hasObjectPayloadError = ReportObjectPayloadError(node, errors);

        if (spec is null || hasObjectPayloadError)
            return null;

        return node.Operator.IsHigherOrder() ? spec : Decorate(node, spec);
    }

    public static SpecBase<TModel, string>? BindOperator<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors) =>
        node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, registry, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], registry, errors)?.Not(),
            _ when node.Operator.IsHigherOrder() => BindHigherOrder<TModel>(node, registry, errors),
            _ => BindComposition<TModel>(node, registry, errors)
        };
```

2. Delete `ReanchorToModel` and replace its two uses with `HigherOrderModelResolver.Reanchor<TModel, TElement, string>(...)` (added below).

Add to `src/Motiv.Serialization/HigherOrderModelResolver.cs`:

```csharp
    public static SpecBase<TModel, TMetadata> Reanchor<TModel, TElement, TMetadata>(
        SpecBase<IEnumerable<TElement>, TMetadata> higherOrder,
        HigherOrderModelResolution resolution)
    {
        if (resolution.Properties.Length > 0)
            return higherOrder.ChangeModelTo<TModel>(model =>
                (IEnumerable<TElement>)resolution.GetCollection(model!));

        return typeof(TModel) == typeof(IEnumerable<TElement>)
            ? (SpecBase<TModel, TMetadata>)(object)higherOrder
            : higherOrder.ChangeModelTo<TModel>(model => (IEnumerable<TElement>)(object)model!);
    }
```

Create `src/Motiv.Serialization/MetadataRuleBinder.cs`:

```csharp
using System.Reflection;
using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class MetadataRuleBinder<TMetadata>(SpecRegistry registry, RuleSerializerOptions options)
{
    private static readonly MethodInfo BindHigherOrderCoreMethod = typeof(MetadataRuleBinder<TMetadata>)
        .GetMethod(nameof(BindHigherOrderCore), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public SpecBase<TModel, TMetadata>? Bind<TModel>(RuleDocument document, List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    private SpecBase<TModel, TMetadata>? BindNode<TModel>(RuleNode node, List<RuleError> errors)
    {
        if (node.WhenTrueText is not null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                $"string 'whenTrue'/'whenFalse' payloads cannot supply metadata of type " +
                $"'{typeof(TMetadata).Name}'; use object payloads"));
            return null;
        }

        if (node.Operator.IsHigherOrder())
            return BindHigherOrder<TModel>(node, errors);

        if (node.HasObjectPayloads)
            return BindRemetadatized<TModel>(node, errors);

        var spec = node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], errors)?.Not(),
            _ => BindComposition<TModel>(node, errors)
        };

        if (spec is null)
            return null;

        return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
    }

    private SpecBase<TModel, TMetadata>? BindRemetadatized<TModel>(RuleNode node, List<RuleError> errors)
    {
        // Parse guarantees a name accompanies object payloads. The operator subtree binds with
        // explanation semantics; the payloads then re-metadatize it, exactly like
        // Spec.Build(spec).WhenTrue(obj).WhenFalse(obj).Create(name) does in core.
        var errorCountBefore = errors.Count;
        var whenTrue = DeserializePayload(node.WhenTrueElement!.Value, $"{node.Path}.whenTrue", errors);
        var whenFalse = DeserializePayload(node.WhenFalseElement!.Value, $"{node.Path}.whenFalse", errors);
        var underlying = RuleBinder.BindOperator<TModel>(node, registry, errors);

        if (underlying is null || errors.Count > errorCountBefore)
            return null;

        return Spec.Build(underlying).WhenTrue(whenTrue!).WhenFalse(whenFalse!).Create(node.Name!);
    }

    private SpecBase<TModel, TMetadata>? BindSpecLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        var entry = registry.Find(node.SpecName!);
        if (entry is null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.UnknownSpec,
                $"no spec is registered under the name '{node.SpecName}'"));
            return null;
        }

        if (entry.IsAsync)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.AsyncSpecInSyncLoad,
                $"'{node.SpecName}' is an async spec; use DeserializeAsyncSpec to load this document"));
            return null;
        }

        if (entry.Spec is not SpecBase<TModel> spec)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                $"loaded for model type '{typeof(TModel).Name}'"));
            return null;
        }

        if (spec is not SpecBase<TModel, TMetadata> typed)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                $"'{node.SpecName}' has metadata type '{entry.MetadataType.Name}' but the load expects " +
                $"'{typeof(TMetadata).Name}'; decorate the node with object 'whenTrue'/'whenFalse' " +
                "payloads or register a matching spec"));
            return null;
        }

        return typed;
    }

    private static SpecBase<TModel, TMetadata>? BindExpressionLeaf<TModel>(
        RuleNode node,
        List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private SpecBase<TModel, TMetadata>? BindComposition<TModel>(RuleNode node, List<RuleError> errors)
    {
        var children = node.Children
            .Select(child => BindNode<TModel>(child, errors))
            .ToArray();

        if (children.Any(child => child is null))
            return null;

        return children.Aggregate((left, right) => node.Operator switch
        {
            RuleOperator.And => left!.And(right!),
            RuleOperator.Or => left!.Or(right!),
            RuleOperator.XOr => left!.XOr(right!),
            RuleOperator.AndAlso => left!.AndAlso(right!),
            _ => left!.OrElse(right!)
        });
    }

    private SpecBase<TModel, TMetadata>? BindHigherOrder<TModel>(RuleNode node, List<RuleError> errors)
    {
        var resolution = HigherOrderModelResolver.Resolve(typeof(TModel), node, errors);
        if (resolution is null)
            return null;

        return (SpecBase<TModel, TMetadata>?)BindHigherOrderCoreMethod
            .MakeGenericMethod(typeof(TModel), resolution.ElementType)
            .Invoke(this, [node, resolution, errors]);
    }

    private SpecBase<TModel, TMetadata>? BindHigherOrderCore<TModel, TElement>(
        RuleNode node,
        HigherOrderModelResolution resolution,
        List<RuleError> errors)
    {
        if (node.HasObjectPayloads)
        {
            var errorCountBefore = errors.Count;
            var whenTrue = DeserializePayload(node.WhenTrueElement!.Value, $"{node.Path}.whenTrue", errors);
            var whenFalse = DeserializePayload(node.WhenFalseElement!.Value, $"{node.Path}.whenFalse", errors);
            var inner = RuleBinder.BindNode<TElement>(node.Children[0], registry, errors);

            if (inner is null || errors.Count > errorCountBefore)
                return null;

            return HigherOrderModelResolver.Reanchor<TModel, TElement, TMetadata>(
                CreateRemetadatizedHigherOrder(node, inner, whenTrue!, whenFalse!), resolution);
        }

        var innerTyped = BindNode<TElement>(node.Children[0], errors);
        if (innerTyped is null)
            return null;

        return HigherOrderModelResolver.Reanchor<TModel, TElement, TMetadata>(
            CreateHigherOrder(node, innerTyped), resolution);
    }

    private static SpecBase<IEnumerable<TElement>, TMetadata> CreateRemetadatizedHigherOrder<TElement>(
        RuleNode node,
        SpecBase<TElement, string> inner,
        TMetadata whenTrue,
        TMetadata whenFalse) =>
        node.Operator switch
        {
            RuleOperator.AsAllSatisfied =>
                Spec.Build(inner).AsAllSatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            RuleOperator.AsAnySatisfied =>
                Spec.Build(inner).AsAnySatisfied().WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            RuleOperator.AsNSatisfied =>
                Spec.Build(inner).AsNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            RuleOperator.AsAtLeastNSatisfied =>
                Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!),
            _ =>
                Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).WhenTrue(whenTrue).WhenFalse(whenFalse).Create(node.Name!)
        };

    private static SpecBase<IEnumerable<TElement>, TMetadata> CreateHigherOrder<TElement>(
        RuleNode node,
        SpecBase<TElement, TMetadata> inner) =>
        node.Operator switch
        {
            RuleOperator.AsAllSatisfied => Spec.Build(inner).AsAllSatisfied().Create(node.Name!),
            RuleOperator.AsAnySatisfied => Spec.Build(inner).AsAnySatisfied().Create(node.Name!),
            RuleOperator.AsNSatisfied => Spec.Build(inner).AsNSatisfied(node.N!.Value).Create(node.Name!),
            RuleOperator.AsAtLeastNSatisfied =>
                Spec.Build(inner).AsAtLeastNSatisfied(node.N!.Value).Create(node.Name!),
            _ => Spec.Build(inner).AsAtMostNSatisfied(node.N!.Value).Create(node.Name!)
        };

    private TMetadata? DeserializePayload(JsonElement element, string path, List<RuleError> errors)
    {
        try
        {
            var value = element.Deserialize<TMetadata>(options.MetadataJsonOptions);
            if (value is not null)
                return value;

            errors.Add(new RuleError(path, RuleErrorCode.MetadataTypeMismatch,
                $"payload deserialized to null for metadata type '{typeof(TMetadata).Name}'"));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            errors.Add(new RuleError(path, RuleErrorCode.MetadataTypeMismatch,
                $"payload could not be deserialized to metadata type '{typeof(TMetadata).Name}': " +
                exception.Message));
        }

        return default;
    }
}
```

Add to `src/Motiv.Serialization/RuleSerializer.cs` (XML docs mirroring the explanation overloads, with a `<typeparam name="TMetadata">` line: "The metadata type object payloads deserialize to and registry entries must yield."):

```csharp
    public SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(string json) =>
        Deserialize<TModel, TMetadata>(json, (IReadOnlyDictionary<string, object?>?)null);

    public SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(string json, object? parameters) =>
        Deserialize<TModel, TMetadata>(json, RuleParameterResolver.ToDictionary(parameters));

    public SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (typeof(TMetadata) == typeof(string))
            return (SpecBase<TModel, TMetadata>)(object)Deserialize<TModel>(json, parameters);

        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = new MetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document!, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS. If the higher-order equivalence tests fail on `Justification`, compare against what the exact fluent chain produces before adjusting anything — the loaded and fluent specs must go through identical builder calls.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: load rule documents as typed metadata specs"
```

---

### Task 10: Semantic `Validate<TModel>` / `Validate<TModel, TMetadata>`

**Files:**
- Modify: `src/Motiv.Serialization/RuleSerializer.cs`
- Create: `src/Motiv.Serialization.Tests/RuleSemanticValidateTests.cs`

**Interfaces:**
- Consumes: `RuleParameterResolver.ResolveForValidation` (Task 3), `RuleBinder.Bind`, `MetadataRuleBinder<TMetadata>.Bind` (Task 9).
- Produces: `IReadOnlyList<RuleError> Validate<TModel>(string json)` and `IReadOnlyList<RuleError> Validate<TModel, TMetadata>(string json)` — accumulate all errors, never throw. Per the locked semantics: no parameter values are taken, placeholders stand in for required parameters, and binding only runs on structurally clean documents.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/RuleSemanticValidateTests.cs`:

```csharp
namespace Motiv.Serialization.Tests;

public class RuleSemanticValidateTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, RuleMetadataTests.RuleOutcome> HasOutcome { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue(new RuleMetadataTests.RuleOutcome { Code = "POS" })
            .WhenFalse(new RuleMetadataTests.RuleOutcome { Code = "NEG" })
            .Create("has outcome");

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("has-outcome", HasOutcome));

    [Fact]
    public void Should_return_no_errors_for_a_semantically_valid_document()
    {
        // Act
        var errors = CreateSerializer().Validate<int>("""{ "rule": { "spec": "is-positive" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_collect_every_unknown_spec_instead_of_throwing()
    {
        // Arrange
        const string json =
            """{ "rule": { "and": [ { "spec": "nope-1" }, { "spec": "nope-2" } ] } }""";

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(error => error.Code == RuleErrorCode.UnknownSpec);
        errors.Select(error => error.Path)
            .ShouldBe(["$.rule.and[0]", "$.rule.and[1]"]);
    }

    [Fact]
    public void Should_not_report_unsupplied_required_parameters()
    {
        // Arrange — parameter supply is a Deserialize concern; placeholders stand in here
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer" } },
              "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "no" }
            }
            """;

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_report_metadata_mismatches_in_a_metadata_validation()
    {
        // Act
        var errors = CreateSerializer()
            .Validate<int, RuleMetadataTests.RuleOutcome>("""{ "rule": { "spec": "is-positive" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_treat_a_string_metadata_validation_as_an_explanation_validation()
    {
        // Act
        var errors = CreateSerializer()
            .Validate<int, string>("""{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_not_bind_when_the_document_has_structural_errors()
    {
        // Arrange — a malformed sibling node must suppress registry lookups entirely
        const string json =
            """{ "rule": { "and": [ { "spec": "unknown-name" }, { "frobnicate": true } ] } }""";

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.ShouldAllBe(error => error.Code == RuleErrorCode.InvalidNode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSemanticValidateTests"`
Expected: FAIL to compile — the generic `Validate` overloads don't exist.

- [ ] **Step 3: Implement**

Add to `src/Motiv.Serialization/RuleSerializer.cs`:

```csharp
    /// <summary>
    /// Checks a rule document structurally and semantically against the registry for an
    /// explanation load, accumulating every error instead of throwing. Parameter values are not
    /// taken: required parameters are stood in by type-shaped placeholders, so supply errors are
    /// only reported by <see cref="Deserialize{TModel}(string)" />.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All errors found, or an empty list when the document would load.</returns>
    public IReadOnlyList<RuleError> Validate<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            RuleBinder.Bind<TModel>(document, _registry, errors);
        return errors;
    }

    /// <summary>
    /// Checks a rule document structurally and semantically against the registry for a metadata
    /// load, accumulating every error instead of throwing. Parameter values are not taken:
    /// required parameters are stood in by type-shaped placeholders, so supply errors are only
    /// reported by <see cref="Deserialize{TModel, TMetadata}(string)" />.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All errors found, or an empty list when the document would load.</returns>
    public IReadOnlyList<RuleError> Validate<TModel, TMetadata>(string json)
    {
        if (typeof(TMetadata) == typeof(string))
            return Validate<TModel>(json);

        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            new MetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document, errors);
        return errors;
    }

    private RuleDocument? PrepareForValidation(string json, List<RuleError> errors)
    {
        var document = new RuleDocumentParser(_options).Parse(json, errors);
        if (document is null)
            return null;

        var values = RuleParameterResolver.ResolveForValidation(document.Parameters);
        if (document.Root is not null)
            RuleParameterSubstituter.Apply(document.Root, values, errors);
        return document;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: add semantic Validate overloads that accumulate binder errors"
```

---

### Task 11: JSON Schema — encode the new `name` constraints

**Files:**
- Modify: `schemas/rule.v1.json`
- Modify: `src/Motiv.Serialization.Tests/RuleSchemaTests.cs`

**Interfaces:**
- Consumes: the loader rules locked in Tasks 5 and 8 (object payloads require `name`; higher-order nodes require `name` or payloads).
- Produces: a schema that rejects what the loader rejects, keeping "every valid test document validates; invalid documents fail" honest.

- [ ] **Step 1: Update the conformance test data (failing first)**

In `src/Motiv.Serialization.Tests/RuleSchemaTests.cs`:

1. In `ValidDocuments`, replace these rows:
   - `"""{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }"""` →
     `"""{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 }, "name": "coded" } }"""`
   - `"""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "Orders" } }"""` →
     `"""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "Orders", "name": "all" } }"""`
   - `"""{ "rule": { "asAnySatisfied": { "spec": "a" } } }"""` →
     `"""{ "rule": { "asAnySatisfied": { "spec": "a" }, "whenTrue": "some", "whenFalse": "none" } }"""`
   - `"""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 3 } }"""` →
     `"""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 3, "name": "exactly three" } }"""`
   - `"""{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": "@minOrders", "path": "Account.Orders" } }"""` →
     `"""{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": "@minOrders", "path": "Account.Orders", "name": "quota" } }"""`
   - `"""{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 2 } }"""` →
     `"""{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 2, "name": "at most two" } }"""`
2. Append to `InvalidDocuments`:

```csharp
        """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" } } }""",
        """{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": 2, "path": "Orders" } }"""
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSchemaTests"`
Expected: FAIL — the three appended invalid documents still validate against the current schema.

- [ ] **Step 3: Update the schema**

In `schemas/rule.v1.json`:

1. In `$defs.sameKindPayloads.anyOf`, add `"required": ["name"]` to the object-payload branch (the third branch):

```json
        {
          "properties": {
            "whenTrue": { "type": "object" },
            "whenFalse": { "type": "object" }
          },
          "required": ["name"]
        }
```

2. Add to **each** of the five higher-order node definitions (`asAllSatisfiedNode`, `asAnySatisfiedNode`, `asNSatisfiedNode`, `asAtLeastNSatisfiedNode`, `asAtMostNSatisfiedNode`), alongside their existing `required`:

```json
      "anyOf": [{ "required": ["name"] }, { "required": ["whenTrue"] }]
```

For example, `asAllSatisfiedNode` becomes:

```json
    "asAllSatisfiedNode": {
      "properties": {
        "asAllSatisfied": { "$ref": "#/$defs/node" },
        "path": { "$ref": "#/$defs/propertyPath" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["asAllSatisfied"],
      "anyOf": [{ "required": ["name"] }, { "required": ["whenTrue"] }],
      "additionalProperties": false
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests -f net10.0`
Expected: PASS — every parser-accepted test document in the suite still validates, and the rejected ones fail.

- [ ] **Step 5: Run net472 and commit**

Run: `dotnet test src/Motiv.Serialization.Tests -f net472`
Expected: PASS.

```bash
git add schemas/rule.v1.json src/Motiv.Serialization.Tests/RuleSchemaTests.cs
git commit -m "feat: encode payload and higher-order name constraints in the JSON schema"
```

---

### Task 12: Full-solution verification + mandatory simplification review

**Files:**
- None planned — whatever the review surfaces.

- [ ] **Step 1: Run the whole solution test suite (all TFMs)**

Run: `dotnet test Motiv.slnx`
Expected: PASS across net8.0, net9.0, net472/netstandard pairings and net10.0, including the example projects (`Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`) — this plan adds no behavior changes outside `Motiv.Serialization`, so any failure elsewhere means an accidental regression.

- [ ] **Step 2: Spawn the `code-simplifier` agent (mandatory per CLAUDE.md)**

Have it review every file changed on this branch (`git diff --name-only main...HEAD`), looking for: duplication between `RuleBinder` and `MetadataRuleBinder` worth consolidating **without** collapsing the explanation/metadata semantics into branchy abstractions (the codebase intentionally tolerates parallel builder paths — see "Avoid over-DRYing"); long methods in the parser; and naming clarity. Apply accepted improvements, then re-run `dotnet test src/Motiv.Serialization.Tests -f net10.0` and `-f net472`.

- [ ] **Step 3: Commit any simplifications**

```bash
git add -A
git commit -m "refactor: apply code-simplifier review findings"
```

(Skip the commit if the review produced no changes.)

- [ ] **Step 4: Final sanity checks**

- `RuleErrorCode` order must be exactly: `InvalidNode`, `UnknownSpec`, `ModelTypeMismatch`, `MetadataTypeMismatch`, `MixedWhenTrueFalseKinds`, `ExpressionsNotEnabled`, `AsyncSpecInSyncLoad`, `DocumentTooLarge`, `MissingParameter`, `SurplusParameter`, `ParameterTypeMismatch`, `UnknownParameterReference`, `InvalidHigherOrderPath`.
- `git log --oneline main..HEAD` shows one commit per task with the messages above.

---

## Deferred to later plans (do not implement here)

- Expression leaves (`Motiv.Serialization.Expressions`, `@param` inside expression strings, `UseExpressions()`) — Plan 3.
- Export / round-trip (`Serialize`, reverse registry lookup, `UnresolvedSpecStub`) — Plan 4.
- `DeserializeAsyncSpec`, the example rules-engine project, README/docs pages — Plan 5.
- The `ExpressionSpecBase`/`PolicyBase` operator-hiding (`new`) equivalence nuance recorded in the Plan-1 review — becomes relevant when expression leaves become registrable in Plan 3.
