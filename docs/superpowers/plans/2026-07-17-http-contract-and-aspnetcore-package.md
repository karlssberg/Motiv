# HTTP Contract + ASP.NET Core Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the transport-agnostic HTTP contract (`schemas/rules-api.v1.yaml`) and a reference `Motiv.Serialization.AspNetCore` package that exposes `catalog` / `validate` / `evaluate` endpoints from a `SpecRegistry` via a single `app.MapMotivRules(...)` call, so a frontend can browse registered specs, validate rule documents, and evaluate them against sample models.

**Architecture:** A small set of additive changes to the base `Motiv.Serialization` package (registry enumeration + per-spec descriptions, needed by the catalog), plus a new net10.0 ASP.NET Core package. The package captures each evaluable model type behind a closure at registration time (`options.AddModel<TModel>("id")`), so the three minimal-API endpoints dispatch by a runtime `modelType` id with **no reflection**. Endpoint logic reuses the existing `RuleSerializer` (load/validate) and Plan 1's `ResultSerializer` (result projection). The contract is a hand-authored OpenAPI 3.1 document that the integration tests hold the implementation to.

**Tech Stack:** C# / .NET 10, ASP.NET Core minimal APIs (`FrameworkReference Microsoft.AspNetCore.App`), `System.Text.Json`, xUnit + Shouldly + `Microsoft.AspNetCore.TestHost` for in-memory integration tests.

---

## Context for the implementer (read first)

This is **Plan 2** of the rules-engine-frontend initiative. Plan 1 (already merged on this branch) added `ResultSerializer` + `RuleEvaluationResult<TMetadata>` / `ExplanationNode` to `Motiv.Serialization`. The frontend design spec is `docs/superpowers/specs/2026-07-16-rules-engine-frontend-ui-design.md` — read its **Backend** section. This plan implements exactly that section; the TypeScript packages are Plans 3–4.

### Repo conventions (verified — trust these)
- MSBuild is configured repo-wide (`Directory.Build.props`): `ImplicitUsings=enable`, `Nullable=enable`, `LangVersion=latest`, **`TreatWarningsAsErrors=true`**. Every public member needs an XML doc comment (`<summary>`, and `<param>` for record positional parameters) or the build fails on CS1591.
- Central package management is on (`Directory.Packages.props`): projects list `<PackageReference Include="X" />` with **no** version; versions live centrally as `<PackageVersion>`. Adding a new package means adding a `<PackageVersion>` there.
- Projects are aggregated in `Motiv.slnx` (an XML solution). New projects **must** be added there or CI (`dotnet test` on the solution) won't see them.
- `src/examples/*` projects target **net10.0** only. This plan follows that: the new package and its tests are **net10.0-only** for this slice (the design's "net8.0+" can be added later by widening `<TargetFrameworks>`; CI already provisions 8/9/10 runtimes).

### Existing APIs this plan builds on
- `SpecRegistry` (`src/Motiv.Serialization/SpecRegistry.cs`): `Register<TModel,TMetadata>(string name, SpecBase<TModel,TMetadata> spec)` (chainable), `Register<TModel,TMetadata>(string, AsyncSpecBase<TModel,TMetadata>)`, `SpecRegistryEntry? Find(string)`, `int Count`. **No enumeration today — Task 1 adds it.**
- `SpecRegistryEntry` (`src/Motiv.Serialization/SpecRegistryEntry.cs`): `Name`, `ModelType`, `MetadataType`, `IsAsync`, internal `Spec`. **No `Description` today — Task 1 adds it.**
- `RuleSerializer` (`src/Motiv.Serialization/RuleSerializer.cs`): `RuleSerializer(SpecRegistry, RuleSerializerOptions?)`, `IReadOnlyList<RuleError> Validate(string json)` (structural checks only), `SpecBase<TModel,string> Deserialize<TModel>(string json)` (throws `RuleSerializationException` on any structural **or** semantic error, e.g. unknown spec / model-type mismatch).
- `RuleError` (`src/Motiv.Serialization/RuleError.cs`): `string Path`, `RuleErrorCode Code` (an **enum**), `string Message`.
- `RuleSerializationException` (`src/Motiv.Serialization/RuleSerializationException.cs`): `IReadOnlyList<RuleError> Errors`.
- `ResultSerializer` / `RuleEvaluationResult<TMetadata>` / `ExplanationNode` (Plan 1): `ResultSerializer.ToEvaluationResult<TMetadata>(BooleanResultBase<TMetadata>)` → the serializable DTO.
- Evaluate a loaded spec: `SpecBase<TModel,string>.Evaluate(model)` → `BooleanResultBase<string>`.

### Key design decisions (do not deviate without escalating)
- **Semantic validation via deserialize-and-catch.** The base package's `Validate(json)` is structural-only. For real semantic feedback (unknown spec, type mismatch), the validate path runs `Validate(json)` first and, if clean, attempts `Deserialize<TModel>(json)` and catches `RuleSerializationException` to surface `ex.Errors`. (A future generic `Validate<TModel,TMetadata>` in the base package would replace this; out of scope here.)
- **No reflection.** `AddModel<TModel>(id)` captures `TModel` in closures stored on a `ModelBinding`. Endpoints look the binding up by id and invoke the closures.
- **String metadata only** (the Plan-1 load surface). Endpoints operate on `SpecBase<TModel,string>` / `RuleEvaluationResult<string>`.
- **Package owns its JSON shape.** All responses go through the package's own `JsonSerializerOptions` (web camelCase + `JsonStringEnumConverter` so `RuleErrorCode` serializes as its name), independent of host config.

### File structure
Base package (modify):
- `src/Motiv.Serialization/SpecRegistryEntry.cs` — add `Description`.
- `src/Motiv.Serialization/SpecRegistry.cs` — add `Entries` + `description` param on `Register`.

Contract (create):
- `schemas/rules-api.v1.yaml` — the OpenAPI 3.1 contract.

New package `src/Motiv.Serialization.AspNetCore/` (create):
- `Motiv.Serialization.AspNetCore.csproj`
- `MotivRulesOptions.cs` — options; `AddModel<TModel>`; model id ↔ type resolution; JSON options.
- `ModelBinding.cs` — internal; per-model validate/evaluate closures.
- `RulesContracts.cs` — request/response DTOs (`CatalogEntry`, `ValidateRequest`, `EvaluateRequest`, `ValidationResponse`, `ErrorResponse`).
- `MotivRulesEndpoints.cs` — `MapMotivRules` extension + the three endpoint handlers.

New test project `src/Motiv.Serialization.AspNetCore.Tests/` (create):
- `Motiv.Serialization.AspNetCore.Tests.csproj`
- `GlobalUsings.cs`
- `TestApp.cs` — spins up an in-memory host via `Microsoft.AspNetCore.TestHost`.
- `MotivRulesOptionsTests.cs`, `CatalogEndpointTests.cs`, `ValidateEndpointTests.cs`, `EvaluateEndpointTests.cs`.

### Test commands
Base-package tests (Task 1):
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0 --filter "FullyQualifiedName~SpecRegistryTests"
```
ASP.NET package tests (Tasks 3–6):
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj
```
> **Sandbox caveat (same as Plan 1):** only the .NET 10 runtime is installed here, so tests *execute* on net10.0. That is the only target framework for these projects, so it is fully covered.

---

## Task 1: Registry descriptions + enumeration (base package)

**Files:**
- Modify: `src/Motiv.Serialization/SpecRegistryEntry.cs`
- Modify: `src/Motiv.Serialization/SpecRegistry.cs`
- Test: `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `src/Motiv.Serialization.Tests/SpecRegistryTests.cs` (inside the existing `SpecRegistryTests` class; it already `uses` the package and Shouldly via GlobalUsings):

```csharp
    [Fact]
    public void Should_expose_registered_entries_with_their_descriptions()
    {
        // Arrange
        var isPositive = Spec.Build((int n) => n > 0).WhenTrue("yes").WhenFalse("no").Create();
        var isEven = Spec.Build((int n) => n % 2 == 0).WhenTrue("yes").WhenFalse("no").Create();

        var registry = new SpecRegistry()
            .Register("is-positive", isPositive, "Whether the number is positive")
            .Register("is-even", isEven);

        // Act
        var entries = registry.Entries.OrderBy(e => e.Name).ToArray();

        // Assert
        entries.Length.ShouldBe(2);
        entries[0].Name.ShouldBe("is-even");
        entries[0].Description.ShouldBeNull();
        entries[1].Name.ShouldBe("is-positive");
        entries[1].Description.ShouldBe("Whether the number is positive");
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0 --filter "FullyQualifiedName~SpecRegistryTests"
```
Expected: FAILS to compile — `Register(string, spec, string)` and `Entries`/`Description` do not exist.

- [ ] **Step 3: Add `Description` to `SpecRegistryEntry`**

In `src/Motiv.Serialization/SpecRegistryEntry.cs`, change the constructor to accept a description and add the property. Replace the constructor and add the property so the file reads:

```csharp
    internal SpecRegistryEntry(string name, Type modelType, Type metadataType, bool isAsync, object spec, string? description = null)
    {
        Name = name;
        ModelType = modelType;
        MetadataType = metadataType;
        IsAsync = isAsync;
        Spec = spec;
        Description = description;
    }
```

Add this property alongside the others (e.g. after `IsAsync`):

```csharp
    /// <summary>An optional human-readable description surfaced in a catalog UI.</summary>
    public string? Description { get; }
```

- [ ] **Step 4: Add `Entries` and the `description` parameter to `SpecRegistry`**

In `src/Motiv.Serialization/SpecRegistry.cs`:

Add an enumeration property after `Count`:

```csharp
    /// <summary>All registered entries. Intended for read-only catalog enumeration after population.</summary>
    public IReadOnlyCollection<SpecRegistryEntry> Entries => _entries.Values;
```

Change both public `Register` methods to accept an optional description and forward it. Replace the two `Register` methods with:

```csharp
    /// <summary>Registers a synchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">The stable name that rule documents use to reference the spec.</param>
    /// <param name="spec">The spec to register.</param>
    /// <param name="description">An optional human-readable description surfaced in a catalog UI.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, SpecBase<TModel, TMetadata> spec, string? description = null) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: false, description);

    /// <summary>Registers an asynchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">The stable name that rule documents use to reference the spec.</param>
    /// <param name="spec">The spec to register.</param>
    /// <param name="description">An optional human-readable description surfaced in a catalog UI.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, AsyncSpecBase<TModel, TMetadata> spec, string? description = null) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: true, description);
```

Change the private `Add` to thread the description through. Replace its signature and the entry construction:

```csharp
    private SpecRegistry Add(string name, object? spec, Type modelType, Type metadataType, bool isAsync, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A registered spec name must not be empty or whitespace.", nameof(name));
        if (spec is null)
            throw new ArgumentNullException(nameof(spec));
        if (_entries.ContainsKey(name))
            throw new ArgumentException($"A spec is already registered under the name '{name}'.", nameof(name));

        _entries[name] = new SpecRegistryEntry(name, modelType, metadataType, isAsync, spec, description);
        return this;
    }
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0 --filter "FullyQualifiedName~SpecRegistryTests"
```
Expected: PASS (the new test plus all pre-existing `SpecRegistryTests`).

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization/SpecRegistryEntry.cs \
        src/Motiv.Serialization/SpecRegistry.cs \
        src/Motiv.Serialization.Tests/SpecRegistryTests.cs
git commit -m "feat: registry entry enumeration and optional descriptions"
```

---

## Task 2: Author the OpenAPI contract

**Files:**
- Create: `schemas/rules-api.v1.yaml`

This is the transport-agnostic source of truth. It is a committed artifact; the integration tests in Tasks 4–6 hold the implementation to it. No code test here.

- [ ] **Step 1: Create `schemas/rules-api.v1.yaml`**

```yaml
openapi: 3.1.0
info:
  title: Motiv Rules API
  version: 1.0.0
  description: >-
    Transport-agnostic contract for a Motiv-backed rules-engine frontend.
    A backend exposes a catalog of registered specifications, validates rule
    documents (see rule.v1.json), and evaluates them against sample models.
    Paths are relative to the base path the host mounts the API under
    (e.g. /api/rules).
paths:
  /catalog:
    get:
      operationId: getCatalog
      summary: List registered specifications.
      responses:
        '200':
          description: The catalog of registered specifications.
          content:
            application/json:
              schema:
                type: array
                items: { $ref: '#/components/schemas/CatalogEntry' }
  /validate:
    post:
      operationId: validateRule
      summary: Validate a rule document against a registered model type.
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/ValidateRequest' }
      responses:
        '200':
          description: Validation completed; errors is empty when the document is valid.
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ValidationResponse' }
        '400':
          description: The model type is not registered.
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ErrorResponse' }
  /evaluate:
    post:
      operationId: evaluateRule
      summary: Evaluate a rule document against a sample model.
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/EvaluateRequest' }
      responses:
        '200':
          description: The evaluation result.
          content:
            application/json:
              schema: { $ref: '#/components/schemas/EvaluationResult' }
        '400':
          description: >-
            The model type is not registered, the rule document is invalid, or
            the sample model could not be bound. A ValidationResponse is returned
            when the rule document is invalid; otherwise an ErrorResponse.
          content:
            application/json:
              schema:
                oneOf:
                  - $ref: '#/components/schemas/ValidationResponse'
                  - $ref: '#/components/schemas/ErrorResponse'
components:
  schemas:
    CatalogEntry:
      type: object
      required: [name, modelType, metadataType, isAsync]
      properties:
        name: { type: string, description: The stable name documents reference the spec by. }
        modelType: { type: string, description: The registered model-type id, or the CLR type name when not registered for evaluation. }
        metadataType: { type: string, description: The metadata type name (e.g. String). }
        isAsync: { type: boolean }
        description: { type: [string, 'null'] }
    ValidateRequest:
      type: object
      required: [modelType, document]
      properties:
        modelType: { type: string, description: A model-type id registered on the server. }
        document: { type: object, description: 'A rule document; see rule.v1.json.' }
    EvaluateRequest:
      type: object
      required: [modelType, document, model]
      properties:
        modelType: { type: string }
        document: { type: object, description: 'A rule document; see rule.v1.json.' }
        model: { description: A sample model instance to evaluate the rule against (any JSON shaped like the model type). }
    RuleError:
      type: object
      required: [path, code, message]
      properties:
        path: { type: string, description: 'JSON path of the offending node, e.g. $.rule.and[1].whenTrue.' }
        code:
          type: string
          enum: [InvalidNode, UnknownSpec, ModelTypeMismatch, MetadataTypeMismatch, MixedWhenTrueFalseKinds, ExpressionsNotEnabled, AsyncSpecInSyncLoad, DocumentTooLarge]
        message: { type: string }
    ValidationResponse:
      type: object
      required: [errors]
      properties:
        errors:
          type: array
          items: { $ref: '#/components/schemas/RuleError' }
    ErrorResponse:
      type: object
      required: [error]
      properties:
        error: { type: string }
    ExplanationNode:
      type: object
      required: [assertions, underlying]
      properties:
        assertions: { type: array, items: { type: string } }
        underlying:
          type: array
          items: { $ref: '#/components/schemas/ExplanationNode' }
    EvaluationResult:
      type: object
      required: [satisfied, reason, assertions, values, justification, explanation]
      properties:
        satisfied: { type: boolean }
        reason: { type: string }
        assertions: { type: array, items: { type: string } }
        values: { type: array, items: { type: string } }
        justification: { type: string }
        explanation: { $ref: '#/components/schemas/ExplanationNode' }
```

- [ ] **Step 2: Commit**

```bash
git add schemas/rules-api.v1.yaml
git commit -m "docs: OpenAPI 3.1 contract for the rules API"
```

---

## Task 3: Scaffold the ASP.NET Core package, options, and test harness

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/Motiv.Serialization.AspNetCore.csproj`
- Create: `src/Motiv.Serialization.AspNetCore/ModelBinding.cs`
- Create: `src/Motiv.Serialization.AspNetCore/MotivRulesOptions.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/GlobalUsings.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/MotivRulesOptionsTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `Motiv.slnx`

- [ ] **Step 1: Create the package project file**

`src/Motiv.Serialization.AspNetCore/Motiv.Serialization.AspNetCore.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <IsPackable>true</IsPackable>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <PackageId>Motiv.Serialization.AspNetCore</PackageId>
        <Title>Motiv.Serialization.AspNetCore</Title>
        <Description>ASP.NET Core minimal-API endpoints (catalog, validate, evaluate) that expose a Motiv SpecRegistry to a rules-engine frontend.</Description>
        <PackageTags>Motiv, Rules Engine, ASP.NET Core, JSON, Specification Pattern</PackageTags>
        <PackageReadmeFile>README.md</PackageReadmeFile>
        <PackageIcon>icon.png</PackageIcon>
        <TargetFramework>net10.0</TargetFramework>
        <MinVerTagPrefix>v</MinVerTagPrefix>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MinVer" PrivateAssets="all" />
    </ItemGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <ItemGroup>
        <InternalsVisibleTo Include="$(AssemblyName).Tests" />
    </ItemGroup>

    <ItemGroup>
        <None Include="..\..\README.md" Pack="true" PackagePath="\" />
        <None Include="..\..\LICENSE" Pack="true" PackagePath="\" />
        <None Include="..\..\icon.png" Pack="true" PackagePath="\" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Motiv.Serialization\Motiv.Serialization.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create `ModelBinding.cs`**

`src/Motiv.Serialization.AspNetCore/ModelBinding.cs`:

```csharp
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Captures a registered evaluable model type <c>TModel</c> behind closures, so the endpoints can
/// validate and evaluate documents for that model without reflection.
/// </summary>
internal sealed class ModelBinding
{
    public required string Id { get; init; }

    public required Type ModelType { get; init; }

    /// <summary>Validates a raw rule-document JSON string, returning all errors (empty when valid).</summary>
    public required Func<RuleSerializer, string, IReadOnlyList<RuleError>> Validate { get; init; }

    /// <summary>
    /// Loads the document, binds the sample model element to <c>TModel</c>, evaluates, and projects
    /// the result. Throws <see cref="RuleSerializationException"/> when the document is invalid and
    /// <see cref="InvalidModelException"/> when the sample model cannot be bound.
    /// </summary>
    public required Func<RuleSerializer, ResultSerializer, JsonSerializerOptions, string, JsonElement, RuleEvaluationResult<string>> Evaluate { get; init; }
}

/// <summary>Thrown when a sample model element cannot be bound to the target model type.</summary>
internal sealed class InvalidModelException(string modelType)
    : Exception($"The supplied model could not be bound to model type '{modelType}'.")
{
    public string ModelType { get; } = modelType;
}
```

- [ ] **Step 3: Create `MotivRulesOptions.cs`**

`src/Motiv.Serialization.AspNetCore/MotivRulesOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Configures the Motiv rules endpoints: the evaluable models and JSON behavior.</summary>
public sealed class MotivRulesOptions
{
    private readonly Dictionary<string, ModelBinding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _idByType = new();

    /// <summary>
    /// JSON options used to read sample models and write all responses. Defaults to web (camelCase)
    /// conventions with enums serialized as their names (so error codes are strings).
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Options forwarded to the underlying <see cref="RuleSerializer"/>, or <c>null</c> for defaults.</summary>
    public RuleSerializerOptions? SerializerOptions { get; set; }

    /// <summary>Registers a model type as evaluable under a stable id used by the endpoints and catalog.</summary>
    /// <typeparam name="TModel">The model type documents evaluate against.</typeparam>
    /// <param name="id">The stable id clients pass as <c>modelType</c>.</param>
    /// <returns>This options instance, to allow chained registration.</returns>
    public MotivRulesOptions AddModel<TModel>(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A model id must not be empty or whitespace.", nameof(id));
        if (_bindings.ContainsKey(id))
            throw new ArgumentException($"A model is already registered under the id '{id}'.", nameof(id));

        _bindings[id] = new ModelBinding
        {
            Id = id,
            ModelType = typeof(TModel),
            Validate = static (serializer, documentJson) =>
            {
                var structural = serializer.Validate(documentJson);
                if (structural.Count > 0) return structural;
                try
                {
                    serializer.Deserialize<TModel>(documentJson);
                    return Array.Empty<RuleError>();
                }
                catch (RuleSerializationException ex)
                {
                    return ex.Errors;
                }
            },
            Evaluate = static (serializer, resultSerializer, jsonOptions, documentJson, modelElement) =>
            {
                var spec = serializer.Deserialize<TModel>(documentJson);
                var model = modelElement.Deserialize<TModel>(jsonOptions)
                            ?? throw new InvalidModelException(typeof(TModel).Name);
                var result = spec.Evaluate(model);
                return resultSerializer.ToEvaluationResult(result);
            }
        };
        _idByType[typeof(TModel)] = id;
        return this;
    }

    internal bool TryGetBinding(string id, out ModelBinding binding) =>
        _bindings.TryGetValue(id, out binding!);

    /// <summary>Resolves a spec's model type to its registered id, falling back to the CLR type name.</summary>
    internal string ResolveModelId(Type modelType) =>
        _idByType.TryGetValue(modelType, out var id) ? id : modelType.Name;
}
```

> Note: `AddModel<TModel>` uses `static` lambdas that reference `TModel` — the type parameter is captured structurally at the generic method's instantiation, so no per-call closure allocation is needed and there is no reflection.

- [ ] **Step 4: Add the TestHost package version**

In `Directory.Packages.props`, add this line inside the `<!-- Test packages -->` group:

```xml
    <PackageVersion Include="Microsoft.AspNetCore.TestHost" Version="10.0.7" />
```

(If restore reports that exact patch is unavailable, use the latest `10.0.x` patch — do not change the major/minor.)

- [ ] **Step 5: Create the test project file**

`src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <OutputType>Library</OutputType>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.TestHost" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="Shouldly" />
        <PackageReference Include="xunit" />
        <PackageReference Include="xunit.runner.visualstudio">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
        <PackageReference Include="coverlet.collector">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Motiv.Serialization.AspNetCore\Motiv.Serialization.AspNetCore.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 6: Create `GlobalUsings.cs`**

`src/Motiv.Serialization.AspNetCore.Tests/GlobalUsings.cs`:

```csharp
global using Motiv;
global using Motiv.Serialization;
global using Motiv.Serialization.AspNetCore;
global using Shouldly;
global using Xunit;
```

- [ ] **Step 7: Write the options unit test (proves the harness builds)**

`src/Motiv.Serialization.AspNetCore.Tests/MotivRulesOptionsTests.cs`:

```csharp
namespace Motiv.Serialization.AspNetCore.Tests;

public class MotivRulesOptionsTests
{
    [Fact]
    public void Should_resolve_a_registered_model_id_and_fall_back_to_the_type_name()
    {
        // Arrange
        var options = new MotivRulesOptions().AddModel<int>("number");

        // Act & Assert
        options.ResolveModelId(typeof(int)).ShouldBe("number");
        options.ResolveModelId(typeof(string)).ShouldBe("String");
    }

    [Fact]
    public void Should_reject_a_duplicate_model_id()
    {
        // Arrange
        var options = new MotivRulesOptions().AddModel<int>("number");

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.AddModel<long>("number"));
    }
}
```

`ResolveModelId` and `TryGetBinding` are `internal`; the test project sees them via the `InternalsVisibleTo` in the package csproj.

- [ ] **Step 8: Register both projects in `Motiv.slnx`**

In `Motiv.slnx`, add these two lines alongside the other top-level `<Project>` entries (after the `Motiv.Serialization` ones):

```xml
  <Project Path="src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj" />
  <Project Path="src/Motiv.Serialization.AspNetCore/Motiv.Serialization.AspNetCore.csproj" />
```

- [ ] **Step 9: Build and run the options tests**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj
```
Expected: both `MotivRulesOptionsTests` pass; build has zero warnings.

- [ ] **Step 10: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/ src/Motiv.Serialization.AspNetCore.Tests/ \
        Directory.Packages.props Motiv.slnx
git commit -m "feat: scaffold Motiv.Serialization.AspNetCore package and options"
```

---

## Task 4: `MapMotivRules` + catalog endpoint

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs`
- Create: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs`

- [ ] **Step 1: Write the failing integration test**

First create the test host helper `src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>Spins up an in-memory host that mounts the rules endpoints under /api/rules.</summary>
internal static class TestApp
{
    public static async Task<WebApplication> StartAsync(SpecRegistry registry, MotivRulesOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapMotivRules("/api/rules", registry, options);
        await app.StartAsync();
        return app;
    }
}
```

Then `src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class CatalogEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    [Fact]
    public async Task Should_list_registered_specs_with_model_id_and_description()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive, "Whether the number is positive");
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement>();
        entries.GetArrayLength().ShouldBe(1);
        var entry = entries[0];
        entry.GetProperty("name").GetString().ShouldBe("is-positive");
        entry.GetProperty("modelType").GetString().ShouldBe("number");
        entry.GetProperty("metadataType").GetString().ShouldBe("String");
        entry.GetProperty("isAsync").GetBoolean().ShouldBeFalse();
        entry.GetProperty("description").GetString().ShouldBe("Whether the number is positive");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~CatalogEndpointTests"
```
Expected: FAILS to compile — `MapMotivRules` does not exist.

- [ ] **Step 3: Create the contracts**

`src/Motiv.Serialization.AspNetCore/RulesContracts.cs`:

```csharp
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A catalog listing for one registered specification.</summary>
/// <param name="Name">The stable name documents reference the spec by.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the spec evaluates asynchronously.</param>
/// <param name="Description">An optional human-readable description.</param>
public sealed record CatalogEntry(string Name, string ModelType, string MetadataType, bool IsAsync, string? Description);

/// <summary>A request to validate a rule document against a registered model type.</summary>
/// <param name="ModelType">A model-type id registered on the server.</param>
/// <param name="Document">A rule document (see rule.v1.json).</param>
public sealed record ValidateRequest(string ModelType, JsonElement Document);

/// <summary>A request to evaluate a rule document against a sample model.</summary>
/// <param name="ModelType">A model-type id registered on the server.</param>
/// <param name="Document">A rule document (see rule.v1.json).</param>
/// <param name="Model">A sample model instance to evaluate against.</param>
public sealed record EvaluateRequest(string ModelType, JsonElement Document, JsonElement Model);

/// <summary>The outcome of a validation request.</summary>
/// <param name="Errors">All errors found; empty when the document is valid.</param>
public sealed record ValidationResponse(IReadOnlyList<RuleError> Errors);

/// <summary>A simple error envelope for request-level failures (e.g. unknown model type).</summary>
/// <param name="Error">A human-readable description of the failure.</param>
public sealed record ErrorResponse(string Error);
```

- [ ] **Step 4: Create the endpoints with the catalog handler**

`src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Extension methods that mount the Motiv rules endpoints on an ASP.NET Core app.</summary>
public static class MotivRulesEndpoints
{
    /// <summary>
    /// Maps <c>GET {basePath}/catalog</c>, <c>POST {basePath}/validate</c>, and
    /// <c>POST {basePath}/evaluate</c>, backed by the given registry and options.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <param name="registry">The registry of specs documents may reference.</param>
    /// <param name="options">The endpoint options, including evaluable model registrations.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        SpecRegistry registry,
        MotivRulesOptions options)
    {
        var serializer = new RuleSerializer(registry, options.SerializerOptions);
        var resultSerializer = new ResultSerializer();
        var json = options.JsonSerializerOptions;
        var group = endpoints.MapGroup(basePath);

        group.MapGet("/catalog", () =>
        {
            var entries = registry.Entries
                .Select(entry => new CatalogEntry(
                    entry.Name,
                    options.ResolveModelId(entry.ModelType),
                    entry.MetadataType.Name,
                    entry.IsAsync,
                    entry.Description))
                .ToArray();
            return Results.Json(entries, json);
        });

        return endpoints;
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~CatalogEndpointTests"
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/RulesContracts.cs \
        src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs \
        src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs \
        src/Motiv.Serialization.AspNetCore.Tests/CatalogEndpointTests.cs
git commit -m "feat: MapMotivRules with catalog endpoint"
```

---

## Task 5: Validate endpoint

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/ValidateEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/Motiv.Serialization.AspNetCore.Tests/ValidateEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class ValidateEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    private static async Task<WebApplication> StartAsync()
    {
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number");
        return await TestApp.StartAsync(registry, options);
    }

    [Fact]
    public async Task Should_return_no_errors_for_a_valid_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_return_an_unknown_spec_error_for_a_bad_reference()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "does-not-exist" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.GetArrayLength().ShouldBe(1);
        errors[0].GetProperty("code").GetString().ShouldBe("UnknownSpec");
        errors[0].GetProperty("path").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_return_400_for_an_unknown_model_type()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "nope",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().ShouldContain("nope");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~ValidateEndpointTests"
```
Expected: FAIL — there is no `/validate` route yet (404, so the assertions fail).

- [ ] **Step 3: Add the validate handler**

In `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, add this block inside `MapMotivRules`, immediately after the `group.MapGet("/catalog", ...)` block and before `return endpoints;`:

```csharp
        group.MapPost("/validate", (ValidateRequest request) =>
        {
            if (!options.TryGetBinding(request.ModelType, out var binding))
                return Results.Json(
                    new ErrorResponse($"Unknown model type '{request.ModelType}'."), json, statusCode: 400);

            var errors = binding.Validate(serializer, request.Document.GetRawText());
            return Results.Json(new ValidationResponse(errors), json);
        });
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~ValidateEndpointTests"
```
Expected: all three PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs \
        src/Motiv.Serialization.AspNetCore.Tests/ValidateEndpointTests.cs
git commit -m "feat: validate endpoint"
```

---

## Task 6: Evaluate endpoint

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/EvaluateEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/Motiv.Serialization.AspNetCore.Tests/EvaluateEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class EvaluateEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    private static async Task<WebApplication> StartAsync()
    {
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number");
        return await TestApp.StartAsync(registry, options);
    }

    [Fact]
    public async Task Should_evaluate_a_document_and_return_the_result_shape()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement,
            model = JsonDocument.Parse("5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        body.GetProperty("reason").GetString().ShouldBe("is positive");
        body.GetProperty("assertions")[0].GetString().ShouldBe("is positive");
        body.GetProperty("explanation").GetProperty("assertions")[0].GetString().ShouldBe("is positive");
    }

    [Fact]
    public async Task Should_reflect_a_false_outcome()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement,
            model = JsonDocument.Parse("-5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeFalse();
        body.GetProperty("reason").GetString().ShouldBe("is not positive");
    }

    [Fact]
    public async Task Should_return_400_with_errors_for_an_invalid_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "does-not-exist" } }""").RootElement,
            model = JsonDocument.Parse("5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("UnknownSpec");
    }

    [Fact]
    public async Task Should_return_400_for_an_unknown_model_type()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "nope",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement,
            model = JsonDocument.Parse("5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().ShouldContain("nope");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EvaluateEndpointTests"
```
Expected: FAIL — there is no `/evaluate` route yet.

- [ ] **Step 3: Add the evaluate handler**

In `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, add this block inside `MapMotivRules`, immediately after the `group.MapPost("/validate", ...)` block and before `return endpoints;`:

```csharp
        group.MapPost("/evaluate", (EvaluateRequest request) =>
        {
            if (!options.TryGetBinding(request.ModelType, out var binding))
                return Results.Json(
                    new ErrorResponse($"Unknown model type '{request.ModelType}'."), json, statusCode: 400);

            try
            {
                var result = binding.Evaluate(
                    serializer, resultSerializer, json, request.Document.GetRawText(), request.Model);
                return Results.Json(result, json);
            }
            catch (RuleSerializationException ex)
            {
                return Results.Json(new ValidationResponse(ex.Errors), json, statusCode: 400);
            }
            catch (InvalidModelException ex)
            {
                return Results.Json(new ErrorResponse(ex.Message), json, statusCode: 400);
            }
        });
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EvaluateEndpointTests"
```
Expected: all four PASS.

- [ ] **Step 5: Run the full package test suite**

Run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj
```
Expected: every test across all four test classes PASS; zero build warnings.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs \
        src/Motiv.Serialization.AspNetCore.Tests/EvaluateEndpointTests.cs
git commit -m "feat: evaluate endpoint"
```

---

## Task 7: Solution-wide build + mandatory simplification review

**Files:**
- Review: everything created/modified in Tasks 1–6.

- [ ] **Step 1: Verify the whole solution restores and the affected suites pass**

Run:
```bash
dotnet build Motiv.slnx
dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj
```
Expected: solution builds with zero warnings; both suites green on net10.0. (Base-package suites for net8.0/net9.0/net472 build here but may not *execute* in this sandbox — that is the known runtime limitation, not a defect. The new ASP.NET projects are net10.0-only and fully covered.)

- [ ] **Step 2: Spawn the code-simplifier agent**

Per CLAUDE.md, dispatch the `code-simplifier:code-simplifier` agent over the files created/modified in this plan (the two base-package files and everything under `src/Motiv.Serialization.AspNetCore/` and `src/Motiv.Serialization.AspNetCore.Tests/`). Instruct it to preserve the public API (`MapMotivRules`, `MotivRulesOptions`, `AddModel`, the record contracts) and the net10.0-only targeting, and to focus on duplication, convoluted design, and naming.

- [ ] **Step 3: Apply accepted suggestions and re-run**

If the agent proposes changes, apply them, then run:
```bash
dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj
```
Expected: PASS.

- [ ] **Step 4: Commit any changes**

```bash
git add src/Motiv.Serialization.AspNetCore/ src/Motiv.Serialization.AspNetCore.Tests/ \
        src/Motiv.Serialization/
git commit -m "refactor: simplify rules endpoints per review"
```

(Skip this commit if the agent found nothing to change.)

---

## Self-Review Notes (author)

- **Spec coverage (frontend design → Backend section):**
  - *HTTP contract table (catalog/validate/evaluate)* → Tasks 2, 4, 5, 6.
  - *`app.MapMotivRules("/api/rules", registry, options)`* → Task 4 (`MotivRulesEndpoints.MapMotivRules`).
  - *`options.AddModel<Customer>("customer")` + catalog `modelType` ids come from these* → Task 3 (`AddModel<TModel>`, `ResolveModelId`), surfaced in Task 4's catalog.
  - *Spec registration gains an optional `description` for the catalog* → Task 1.
  - *Result serialization in `Motiv.Serialization`* → already delivered by Plan 1; consumed by the evaluate handler in Task 6.
  - *net8.0+ targeting* → deliberately narrowed to **net10.0** for this slice (matches `src/examples/*`; widen `<TargetFramework>`→`<TargetFrameworks>` later). Flagged, not silently dropped.
- **Deferred by design (not in this plan):** persistence/CRUD (host concern); parameters, higher-order, metadata-object, async, and expression documents (backend serialization plans not yet shipped — the load surface is Plan 1's string-explanation subset); user-facing README/docs (frontend spec's final plan); the OpenAPI doc is hand-authored and enforced by integration tests rather than generated.
- **Placeholder scan:** none — every code and command step is concrete.
- **Type/name consistency across tasks:** `MotivRulesOptions`, `AddModel<TModel>`, `TryGetBinding`, `ResolveModelId`, `ModelBinding` (`Validate`/`Evaluate`), `InvalidModelException`, `MapMotivRules`, `CatalogEntry`, `ValidateRequest`, `EvaluateRequest`, `ValidationResponse`, `ErrorResponse`, and the `/api/rules` base path with `/catalog`,`/validate`,`/evaluate` sub-routes are used identically in every task and in the tests. `RuleError`/`RuleErrorCode`/`RuleSerializationException.Errors` match the real base-package types. Response JSON is camelCase with string enum codes (web defaults + `JsonStringEnumConverter`), matching the OpenAPI schemas and the tests' property assertions.
