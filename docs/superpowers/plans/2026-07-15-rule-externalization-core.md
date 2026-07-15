# Rule Externalization Plan 1: Base Package Core — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the `Motiv.Serialization` base package: a `SpecRegistry` of named specs, a JSON rule-document loader that composes them with logical operators and explanation decoration, a structured validation/error model, and the formal JSON Schema for the document format.

**Architecture:** JSON documents are parsed with `System.Text.Json`'s `JsonDocument` into an internal node tree (`RuleDocumentParser` → `RuleNode`), then bound against the registry into real Motiv specs (`RuleBinder` → `SpecBase<TModel, string>` via the public `ToExplanationSpec()` degrade path and the `Spec.Build(spec)` decoration builders). The JSON format is a projection of the fluent builder — every node maps 1:1 to an existing builder operation, and the core test idiom asserts JSON-loaded and fluent-built specs produce identical results. Parameters, higher-order nodes, metadata loads, expressions, export, and async loading are Plans 2–5 (see the design spec's Implementation Decomposition); this plan's parser recognizes-and-rejects those format features with clear errors.

**Tech Stack:** C# (LangVersion latest, nullable enabled), System.Text.Json, xUnit + Shouldly, JsonSchema.Net (test-only).

**Design spec:** `docs/superpowers/specs/2026-07-15-rule-externalization-design.md`

## Global Constraints

- Library TFMs: `net8.0;net9.0;netstandard2.0;net10.0` (same as `Motiv`). Test TFMs: `net8.0;net9.0;net472;net10.0` (same as `Motiv.Tests`).
- Central package management is on (`Directory.Packages.props`); never put `Version=` in a csproj.
- `TreatWarningsAsErrors` is on solution-wide; `Nullable` and `ImplicitUsings` are enabled via `Directory.Build.props`.
- The base package's only dependency beyond `Motiv` is `System.Text.Json` (and only for the `netstandard2.0` target — it is inbox on net8.0+).
- No records or `required`/`init` members in library code — the `netstandard2.0` target has no `IsExternalInit`/`RequiredMemberAttribute` polyfills. Use constructors and `get`-only or `get`/`set` properties.
- Namespace: `Motiv.Serialization` (types from the parent `Motiv` namespace resolve without a `using`).
- XML doc headers on all public types and members.
- TDD: write the failing test, see it fail, implement, see it pass, commit. Run the fast loop on `-f net10.0`; the full multi-TFM run (including an explicit net472 run) happens per task before commit.
- Error codes and messages must match this plan exactly — tests assert on `RuleErrorCode` and `Path` values.

## File Structure

```
schemas/
  rule.v1.json                                  (Task 5 — the format contract)
src/Motiv.Serialization/
  Motiv.Serialization.csproj                    (Task 1)
  SpecRegistry.cs                               (Task 1)
  SpecRegistryEntry.cs                          (Task 1)
  RuleError.cs                                  (Task 2)
  RuleErrorCode.cs                              (Task 2)
  RuleSerializationException.cs                 (Task 2)
  RuleSerializerOptions.cs                      (Task 2)
  RuleSerializer.cs                             (Task 2: Validate; Task 3: Deserialize)
  RuleDocument.cs                               (Task 2 — internal)
  RuleNode.cs                                   (Task 2 — internal)
  RuleOperator.cs                               (Task 2 — internal)
  RuleDocumentParser.cs                         (Task 2 — internal)
  RuleBinder.cs                                 (Task 3: leaves/decoration; Task 4: composition)
src/Motiv.Serialization.Tests/
  Motiv.Serialization.Tests.csproj              (Task 1)
  GlobalUsings.cs                               (Task 1)
  SpecRegistryTests.cs                          (Task 1)
  RuleSerializerValidateTests.cs                (Task 2)
  RuleSerializerDeserializeTests.cs             (Task 3)
  RuleCompositionTests.cs                       (Task 4)
  RuleSchemaTests.cs                            (Task 5)
```

---

### Task 1: Project scaffolding + SpecRegistry

**Files:**
- Create: `src/Motiv.Serialization/Motiv.Serialization.csproj`
- Create: `src/Motiv.Serialization/SpecRegistry.cs`
- Create: `src/Motiv.Serialization/SpecRegistryEntry.cs`
- Create: `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
- Create: `src/Motiv.Serialization.Tests/GlobalUsings.cs`
- Create: `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`
- Modify: `Motiv.slnx` (add both projects)
- Modify: `Directory.Packages.props` (add `System.Text.Json`)

**Interfaces:**
- Consumes: `SpecBase<TModel, TMetadata>`, `AsyncSpecBase<TModel, TMetadata>`, `Spec.Build(...)`, `Spec.BuildAsync(...)` from `Motiv`.
- Produces: `SpecRegistry` with `SpecRegistry Register<TModel, TMetadata>(string name, SpecBase<TModel, TMetadata> spec)`, `SpecRegistry Register<TModel, TMetadata>(string name, AsyncSpecBase<TModel, TMetadata> spec)`, `SpecRegistryEntry? Find(string name)`, `int Count`. `SpecRegistryEntry` with `string Name`, `Type ModelType`, `Type MetadataType`, `bool IsAsync`, and `internal object Spec`. Tasks 3–4 rely on `Find` + the entry shape exactly as declared here.

- [ ] **Step 1: Create the library project**

Write `src/Motiv.Serialization/Motiv.Serialization.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <IsPackable>true</IsPackable>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <PackageId>Motiv.Serialization</PackageId>
        <Title>Motiv.Serialization</Title>
        <Description>Externalize Motiv rules as JSON documents: compose registered specifications with logical operators and explanation text, validated and loaded at runtime into fully functional Motiv specifications.</Description>
        <PackageTags>Motiv, Rules Engine, JSON, Serialization, Specification Pattern, Boolean Logic</PackageTags>
        <PackageReadmeFile>README.md</PackageReadmeFile>
        <PackageIcon>icon.png</PackageIcon>
        <TargetFrameworks>net8.0;net9.0;netstandard2.0;net10.0</TargetFrameworks>
        <MinVerTagPrefix>v</MinVerTagPrefix>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MinVer" PrivateAssets="all" />
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
        <ProjectReference Include="..\Motiv\Motiv.csproj" />
    </ItemGroup>

    <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
        <PackageReference Include="System.Text.Json" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the test project**

Write `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <OutputType>Library</OutputType>
        <TargetFrameworks>net8.0;net9.0;net472;net10.0</TargetFrameworks>
    </PropertyGroup>

    <ItemGroup>
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
        <ProjectReference Include="..\Motiv.Serialization\Motiv.Serialization.csproj" />
    </ItemGroup>

</Project>
```

Write `src/Motiv.Serialization.Tests/GlobalUsings.cs`:

```csharp
global using Motiv;
global using Shouldly;
global using Xunit;
```

- [ ] **Step 3: Register the version and add projects to the solution**

Add to `Directory.Packages.props` in the `<!-- System packages -->` group:

```xml
<PackageVersion Include="System.Text.Json" Version="10.0.2" />
```

(If restore later fails because 10.0.2 does not exist, use the latest 10.0.x listed by `dotnet package search System.Text.Json --exact-match`.)

Run:

```bash
dotnet sln Motiv.slnx add src/Motiv.Serialization/Motiv.Serialization.csproj src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj
```

If `dotnet sln` rejects the slnx, instead add these two lines to `Motiv.slnx` alongside the other `<Project>` entries:

```xml
<Project Path="src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj" />
<Project Path="src/Motiv.Serialization/Motiv.Serialization.csproj" />
```

- [ ] **Step 4: Write the failing SpecRegistry tests**

Write `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class SpecRegistryTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).Create("is positive");

    [Fact]
    public void Should_find_a_registered_spec_by_name()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);

        // Act
        var entry = registry.Find("is-positive");

        // Assert
        entry.ShouldNotBeNull();
        entry.Name.ShouldBe("is-positive");
        entry.ModelType.ShouldBe(typeof(int));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
        entry.Spec.ShouldBeSameAs(IsPositive);
    }

    [Fact]
    public void Should_record_the_metadata_type_of_a_metadata_spec()
    {
        // Arrange
        var hasFlag = Spec
            .Build((int n) => n != 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("has flag");

        var registry = new SpecRegistry().Register("has-flag", hasFlag);

        // Act
        var entry = registry.Find("has-flag");

        // Assert
        entry.ShouldNotBeNull();
        entry.MetadataType.ShouldBe(typeof(int));
    }

    [Fact]
    public void Should_record_async_registrations_as_async()
    {
        // Arrange
        var isPositiveAsync = Spec
            .BuildAsync((int n) => Task.FromResult(n > 0))
            .Create("is positive async");

        var registry = new SpecRegistry().Register("is-positive-async", isPositiveAsync);

        // Act
        var entry = registry.Find("is-positive-async");

        // Assert
        entry.ShouldNotBeNull();
        entry.IsAsync.ShouldBeTrue();
        entry.ModelType.ShouldBe(typeof(int));
    }

    [Fact]
    public void Should_return_null_for_an_unregistered_name()
    {
        // Arrange
        var registry = new SpecRegistry();

        // Act
        var entry = registry.Find("missing");

        // Assert
        entry.ShouldBeNull();
    }

    [Fact]
    public void Should_throw_when_registering_a_duplicate_name()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);

        // Act
        var act = () => registry.Register("is-positive", IsPositive);

        // Assert
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("is-positive");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_throw_when_registering_a_blank_name(string name)
    {
        // Arrange
        var registry = new SpecRegistry();

        // Act
        var act = () => registry.Register(name, IsPositive);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Should_count_registrations()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("is-positive-2", IsPositive);

        // Act & Assert
        registry.Count.ShouldBe(2);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: build FAILS with CS0246 (`SpecRegistry` not found) — the right reason.

- [ ] **Step 6: Implement SpecRegistry and SpecRegistryEntry**

Write `src/Motiv.Serialization/SpecRegistryEntry.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Describes a spec registered with a <see cref="SpecRegistry" />: its stable name, model and
/// metadata types, and whether it evaluates asynchronously.
/// </summary>
public sealed class SpecRegistryEntry
{
    internal SpecRegistryEntry(string name, Type modelType, Type metadataType, bool isAsync, object spec)
    {
        Name = name;
        ModelType = modelType;
        MetadataType = metadataType;
        IsAsync = isAsync;
        Spec = spec;
    }

    /// <summary>The stable name that rule documents use to reference the spec.</summary>
    public string Name { get; }

    /// <summary>The model type the spec evaluates against.</summary>
    public Type ModelType { get; }

    /// <summary>The metadata type the spec yields.</summary>
    public Type MetadataType { get; }

    /// <summary>Whether the spec evaluates asynchronously.</summary>
    public bool IsAsync { get; }

    internal object Spec { get; }
}
```

Write `src/Motiv.Serialization/SpecRegistry.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A catalog of specs registered under stable names. Names are the contract between compiled
/// code and externalized rule documents: documents reference and compose registered specs by name.
/// </summary>
public sealed class SpecRegistry
{
    private readonly Dictionary<string, SpecRegistryEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>The number of registered specs.</summary>
    public int Count => _entries.Count;

    /// <summary>Registers a synchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">The stable name that rule documents use to reference the spec.</param>
    /// <param name="spec">The spec to register.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, SpecBase<TModel, TMetadata> spec) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: false);

    /// <summary>Registers an asynchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">The stable name that rule documents use to reference the spec.</param>
    /// <param name="spec">The spec to register.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, AsyncSpecBase<TModel, TMetadata> spec) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: true);

    /// <summary>Looks up a registered spec by name.</summary>
    /// <param name="name">The name the spec was registered under.</param>
    /// <returns>The matching entry, or <c>null</c> when no spec is registered under the name.</returns>
    public SpecRegistryEntry? Find(string name) =>
        _entries.TryGetValue(name, out var entry) ? entry : null;

    private SpecRegistry Add(string name, object? spec, Type modelType, Type metadataType, bool isAsync)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A registered spec name must not be empty or whitespace.", nameof(name));
        if (spec is null)
            throw new ArgumentNullException(nameof(spec));
        if (_entries.ContainsKey(name))
            throw new ArgumentException($"A spec is already registered under the name '{name}'.", nameof(name));

        _entries[name] = new SpecRegistryEntry(name, modelType, metadataType, isAsync, spec);
        return this;
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: PASS (7 tests).

- [ ] **Step 8: Run all TFMs including net472**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
Expected: PASS on net8.0, net9.0, net472, net10.0. If net472 fails on assembly load, add `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>` to the test csproj's PropertyGroup and re-run.

- [ ] **Step 9: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests Motiv.slnx Directory.Packages.props
git commit -m "feat: add Motiv.Serialization package with SpecRegistry"
```

---

### Task 2: Error model, document parser, and structural Validate

**Files:**
- Create: `src/Motiv.Serialization/RuleError.cs`
- Create: `src/Motiv.Serialization/RuleErrorCode.cs`
- Create: `src/Motiv.Serialization/RuleSerializationException.cs`
- Create: `src/Motiv.Serialization/RuleSerializerOptions.cs`
- Create: `src/Motiv.Serialization/RuleSerializer.cs`
- Create: `src/Motiv.Serialization/RuleOperator.cs`
- Create: `src/Motiv.Serialization/RuleNode.cs`
- Create: `src/Motiv.Serialization/RuleDocument.cs`
- Create: `src/Motiv.Serialization/RuleDocumentParser.cs`
- Test: `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`

**Interfaces:**
- Consumes: `SpecRegistry` (Task 1).
- Produces (public): `RuleError { string Path; RuleErrorCode Code; string Message }`, `enum RuleErrorCode { InvalidNode, UnknownSpec, ModelTypeMismatch, MetadataTypeMismatch, MixedWhenTrueFalseKinds, ExpressionsNotEnabled, AsyncSpecInSyncLoad, DocumentTooLarge }`, `RuleSerializationException { IReadOnlyList<RuleError> Errors }`, `RuleSerializerOptions { int MaxDocumentDepth = 64; int MaxNodeCount = 10_000 }`, `RuleSerializer(SpecRegistry, RuleSerializerOptions? = null)` with `IReadOnlyList<RuleError> Validate(string json)`.
- Produces (internal, consumed by Tasks 3–4): `enum RuleOperator { Spec, Expression, And, Or, XOr, AndAlso, OrElse, Not }`; `RuleNode { RuleOperator Operator; string Path; string? SpecName; string? ExpressionText; List<RuleNode> Children; string? WhenTrueText; string? WhenFalseText; bool HasObjectPayloads; string? Name }`; `RuleDocument { string? Name; RuleNode? Root }`; `RuleDocumentParser(RuleSerializerOptions)` with `RuleDocument? Parse(string json, List<RuleError> errors)`.

- [ ] **Step 1: Write the failing validation tests**

Write `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class RuleSerializerValidateTests
{
    private static IReadOnlyList<RuleError> Validate(string json, RuleSerializerOptions? options = null) =>
        new RuleSerializer(new SpecRegistry(), options).Validate(json);

    [Theory]
    [InlineData("""{ "rule": { "spec": "is-positive" } }""")]
    [InlineData("""{ "$schema": "https://example.com/rule.v1.json", "name": "doc", "rule": { "spec": "a" } }""")]
    [InlineData("""{ "rule": { "and": [ { "spec": "a" }, { "not": { "spec": "b" } } ] } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": "no", "name": "n" } }""")]
    [InlineData("""{ "rule": { "expression": "Age >= 18" } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""")]
    [InlineData("""{ "parameters": { "minAge": { "type": "integer", "default": 18 } }, "rule": { "spec": "a" } }""")]
    public void Should_report_no_errors_for_structurally_valid_documents(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not json at all", "$")]
    [InlineData("[]", "$")]
    [InlineData("\"rule\"", "$")]
    public void Should_reject_documents_that_are_not_json_objects(string json, string expectedPath)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(expectedPath);
    }

    [Fact]
    public void Should_reject_a_document_without_a_rule()
    {
        // Act
        var errors = Validate("""{ "name": "doc" }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$");
        error.Message.ShouldContain("rule");
    }

    [Fact]
    public void Should_reject_an_unknown_envelope_property()
    {
        // Act
        var errors = Validate("""{ "frobnicate": 1, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.frobnicate");
    }

    [Fact]
    public void Should_reject_a_node_with_no_operator()
    {
        // Act
        var errors = Validate("""{ "rule": { "name": "empty" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("exactly one");
    }

    [Fact]
    public void Should_reject_a_node_with_two_operators()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "expression": "Age >= 18" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_reject_an_unknown_node_property()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "frobnicate": true } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.frobnicate");
        error.Message.ShouldContain("unknown property");
    }

    [Fact]
    public void Should_explain_that_higher_order_properties_are_not_yet_supported()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" } } }""");

        // Assert
        errors.ShouldContain(error =>
            error.Code == RuleErrorCode.InvalidNode &&
            error.Path == "$.rule.asAllSatisfied" &&
            error.Message.Contains("not yet supported"));
    }

    [Theory]
    [InlineData("""{ "rule": { "and": { "spec": "a" } } }""")]
    [InlineData("""{ "rule": { "and": [ { "spec": "a" } ] } }""")]
    [InlineData("""{ "rule": { "or": [] } }""")]
    public void Should_reject_binary_operators_without_at_least_two_operands(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Message.ShouldContain("at least two");
    }

    [Fact]
    public void Should_reject_a_not_operator_that_is_not_an_object()
    {
        // Act
        var errors = Validate("""{ "rule": { "not": [ { "spec": "a" } ] } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.not");
    }

    [Fact]
    public void Should_reject_whenTrue_without_whenFalse()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "whenTrue": "yes" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("together");
    }

    [Fact]
    public void Should_reject_mixed_payload_kinds()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": { "code": 2 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MixedWhenTrueFalseKinds);
        error.Path.ShouldBe("$.rule");
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": 1, "whenFalse": 2 } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": true, "whenFalse": false } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": ["yes"], "whenFalse": ["no"] } }""")]
    public void Should_reject_payloads_that_are_neither_strings_nor_objects(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldAllBe(error => error.Code == RuleErrorCode.InvalidNode);
        errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "" } }""", "$.rule.spec")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": " ", "whenFalse": "no" } }""", "$.rule.whenTrue")]
    [InlineData("""{ "rule": { "spec": "a", "name": "" } }""", "$.rule.name")]
    [InlineData("""{ "name": " ", "rule": { "spec": "a" } }""", "$.name")]
    public void Should_reject_blank_strings(string json, string expectedPath)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData("""{ "parameters": [ "minAge" ], "rule": { "spec": "a" } }""", "$.parameters")]
    [InlineData("""{ "parameters": { "minAge": { "default": 18 } }, "rule": { "spec": "a" } }""", "$.parameters.minAge")]
    [InlineData("""{ "parameters": { "minAge": { "type": "decimal" } }, "rule": { "spec": "a" } }""", "$.parameters.minAge.type")]
    [InlineData("""{ "parameters": { "minAge": { "type": "integer", "frobnicate": 1 } }, "rule": { "spec": "a" } }""", "$.parameters.minAge.frobnicate")]
    public void Should_validate_parameter_declarations(string json, string expectedPath)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(expectedPath);
    }

    [Fact]
    public void Should_reject_a_document_that_exceeds_the_maximum_depth()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxDocumentDepth = 3 };
        const string json =
            """{ "rule": { "not": { "not": { "not": { "spec": "a" } } } } }""";

        // Act
        var errors = Validate(json, options);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.DocumentTooLarge);
    }

    [Fact]
    public void Should_reject_a_document_that_exceeds_the_maximum_node_count()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxNodeCount = 2 };
        const string json =
            """{ "rule": { "and": [ { "spec": "a" }, { "spec": "b" }, { "spec": "c" } ] } }""";

        // Act
        var errors = Validate(json, options);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.DocumentTooLarge);
    }

    [Fact]
    public void Should_report_multiple_errors_in_one_pass()
    {
        // Act
        var errors = Validate(
            """{ "frobnicate": 1, "rule": { "and": [ { "spec": "" }, { "name": "no operator" } ] } }""");

        // Assert
        errors.Count.ShouldBe(3);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: build FAILS with CS0246 (`RuleSerializer`, `RuleError`, `RuleSerializerOptions` not found).

- [ ] **Step 3: Implement the error model and options**

Write `src/Motiv.Serialization/RuleErrorCode.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>Stable machine-readable codes for rule-document errors.</summary>
public enum RuleErrorCode
{
    /// <summary>The document or a node within it is structurally invalid.</summary>
    InvalidNode,

    /// <summary>The document references a spec name that is not registered.</summary>
    UnknownSpec,

    /// <summary>A referenced spec's model type does not match the model type of the load.</summary>
    ModelTypeMismatch,

    /// <summary>A node's metadata cannot be reconciled with the metadata type of the load.</summary>
    MetadataTypeMismatch,

    /// <summary>A node's whenTrue and whenFalse payloads are of different kinds.</summary>
    MixedWhenTrueFalseKinds,

    /// <summary>The document uses expression nodes but expression support is not enabled.</summary>
    ExpressionsNotEnabled,

    /// <summary>A synchronous load referenced an asynchronous registry entry.</summary>
    AsyncSpecInSyncLoad,

    /// <summary>The document exceeds the configured depth or node-count limits.</summary>
    DocumentTooLarge
}
```

Write `src/Motiv.Serialization/RuleError.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>A single validation or load error found in a rule document.</summary>
public sealed class RuleError
{
    /// <summary>Creates a rule error.</summary>
    /// <param name="path">The JSON path of the offending element, e.g. <c>$.rule.and[1].whenTrue</c>.</param>
    /// <param name="code">The stable machine-readable error code.</param>
    /// <param name="message">The human-readable description of the error.</param>
    public RuleError(string path, RuleErrorCode code, string message)
    {
        Path = path;
        Code = code;
        Message = message;
    }

    /// <summary>The JSON path of the offending element, e.g. <c>$.rule.and[1].whenTrue</c>.</summary>
    public string Path { get; }

    /// <summary>The stable machine-readable error code.</summary>
    public RuleErrorCode Code { get; }

    /// <summary>The human-readable description of the error.</summary>
    public string Message { get; }

    /// <summary>Formats the error as <c>Code at Path: Message</c>.</summary>
    /// <returns>The formatted error.</returns>
    public override string ToString() => $"{Code} at {Path}: {Message}";
}
```

Write `src/Motiv.Serialization/RuleSerializationException.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>Thrown when a rule document cannot be deserialized into a spec.</summary>
public sealed class RuleSerializationException : Exception
{
    /// <summary>Creates the exception from the errors found in the document.</summary>
    /// <param name="errors">The errors found in the document.</param>
    public RuleSerializationException(IReadOnlyList<RuleError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>All errors found in the document.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<RuleError> errors) =>
        errors.Count switch
        {
            0 => "The rule document is invalid.",
            1 => errors[0].ToString(),
            _ => $"{errors[0]} (+{errors.Count - 1} more)"
        };
}
```

Write `src/Motiv.Serialization/RuleSerializerOptions.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>Options that control how rule documents are validated and loaded.</summary>
public sealed class RuleSerializerOptions
{
    /// <summary>The maximum nesting depth a rule document may have. Defaults to 64.</summary>
    public int MaxDocumentDepth { get; set; } = 64;

    /// <summary>The maximum number of rule nodes a document may contain. Defaults to 10,000.</summary>
    public int MaxNodeCount { get; set; } = 10_000;
}
```

- [ ] **Step 4: Implement the internal document model**

Write `src/Motiv.Serialization/RuleOperator.cs`:

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
    Not
}
```

Write `src/Motiv.Serialization/RuleNode.cs`:

```csharp
namespace Motiv.Serialization;

internal sealed class RuleNode(RuleOperator @operator, string path)
{
    public RuleOperator Operator { get; } = @operator;

    public string Path { get; } = path;

    public string? SpecName { get; set; }

    public string? ExpressionText { get; set; }

    public List<RuleNode> Children { get; } = [];

    public string? WhenTrueText { get; set; }

    public string? WhenFalseText { get; set; }

    // Plan 2 replaces this flag with retained JsonElement payloads for typed metadata binding.
    public bool HasObjectPayloads { get; set; }

    public string? Name { get; set; }
}
```

Write `src/Motiv.Serialization/RuleDocument.cs`:

```csharp
namespace Motiv.Serialization;

internal sealed class RuleDocument(string? name, RuleNode? root)
{
    public string? Name { get; } = name;

    public RuleNode? Root { get; } = root;
}
```

- [ ] **Step 5: Implement the parser**

Write `src/Motiv.Serialization/RuleDocumentParser.cs`:

```csharp
using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class RuleDocumentParser(RuleSerializerOptions options)
{
    private static readonly string[] HigherOrderProperties =
    [
        "asAllSatisfied", "asAnySatisfied", "asNSatisfied", "asAtLeastNSatisfied", "asAtMostNSatisfied", "n", "path"
    ];

    private static readonly string[] ParameterTypes = ["integer", "number", "string", "boolean"];

    private int _nodeCount;
    private bool _tooLargeReported;

    public RuleDocument? Parse(string json, List<RuleError> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, $"invalid JSON: {exception.Message}"));
            return null;
        }

        using (document)
        {
            return ParseEnvelope(document.RootElement, errors);
        }
    }

    private RuleDocument? ParseEnvelope(JsonElement root, List<RuleError> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, "document must be a JSON object"));
            return null;
        }

        string? name = null;
        RuleNode? rule = null;
        var hasRule = false;

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "$schema":
                    break;
                case "name":
                    name = ReadNonEmptyString(property.Value, "$.name", errors);
                    break;
                case "parameters":
                    ValidateParameterDeclarations(property.Value, errors);
                    break;
                case "rule":
                    hasRule = true;
                    rule = ParseNode(property.Value, "$.rule", depth: 1, errors);
                    break;
                default:
                    errors.Add(new RuleError($"$.{property.Name}", RuleErrorCode.InvalidNode,
                        $"unknown property '{property.Name}'"));
                    break;
            }
        }

        if (!hasRule)
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, "missing required property 'rule'"));

        return new RuleDocument(name, rule);
    }

    private RuleNode? ParseNode(JsonElement element, string path, int depth, List<RuleError> errors)
    {
        if (ExceedsLimits(path, depth, errors))
            return null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode, "rule node must be a JSON object"));
            return null;
        }

        var operators = new List<JsonProperty>();
        JsonElement? whenTrue = null;
        JsonElement? whenFalse = null;
        string? name = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "spec" or "expression" or "not" or "and" or "or" or "xor" or "andAlso" or "orElse":
                    operators.Add(property);
                    break;
                case "whenTrue":
                    whenTrue = property.Value;
                    break;
                case "whenFalse":
                    whenFalse = property.Value;
                    break;
                case "name":
                    name = ReadNonEmptyString(property.Value, $"{path}.name", errors);
                    break;
                default:
                    var message = HigherOrderProperties.Contains(property.Name)
                        ? $"'{property.Name}' is part of the rule format but is not yet supported by this loader"
                        : $"unknown property '{property.Name}'";
                    errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode, message));
                    break;
            }
        }

        if (operators.Count != 1)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "rule node must contain exactly one of 'spec', 'expression', 'not', 'and', 'or', 'xor', " +
                "'andAlso' or 'orElse'"));
            return null;
        }

        var node = ParseOperator(operators[0], path, depth, errors);
        if (node is null)
            return null;

        node.Name = name;
        ParsePayloads(node, whenTrue, whenFalse, path, errors);
        return node;
    }

    private RuleNode? ParseOperator(JsonProperty property, string path, int depth, List<RuleError> errors)
    {
        switch (property.Name)
        {
            case "spec":
            {
                var specName = ReadNonEmptyString(property.Value, $"{path}.spec", errors);
                return specName is null ? null : new RuleNode(RuleOperator.Spec, path) { SpecName = specName };
            }
            case "expression":
            {
                var expression = ReadNonEmptyString(property.Value, $"{path}.expression", errors);
                return expression is null
                    ? null
                    : new RuleNode(RuleOperator.Expression, path) { ExpressionText = expression };
            }
            case "not":
            {
                var child = ParseNode(property.Value, $"{path}.not", depth + 1, errors);
                if (child is null)
                    return null;

                var node = new RuleNode(RuleOperator.Not, path);
                node.Children.Add(child);
                return node;
            }
            default:
                return ParseBinaryOperator(property, path, depth, errors);
        }
    }

    private RuleNode? ParseBinaryOperator(JsonProperty property, string path, int depth, List<RuleError> errors)
    {
        var @operator = property.Name switch
        {
            "and" => RuleOperator.And,
            "or" => RuleOperator.Or,
            "xor" => RuleOperator.XOr,
            "andAlso" => RuleOperator.AndAlso,
            _ => RuleOperator.OrElse
        };

        if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() < 2)
        {
            errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                $"'{property.Name}' must be an array of at least two rule nodes"));
            return null;
        }

        var node = new RuleNode(@operator, path);
        var index = 0;
        foreach (var item in property.Value.EnumerateArray())
        {
            var child = ParseNode(item, $"{path}.{property.Name}[{index}]", depth + 1, errors);
            if (child is not null)
                node.Children.Add(child);
            index++;
        }

        return node.Children.Count == index ? node : null;
    }

    private static void ParsePayloads(
        RuleNode node,
        JsonElement? whenTrue,
        JsonElement? whenFalse,
        string path,
        List<RuleError> errors)
    {
        if (whenTrue is null && whenFalse is null)
            return;

        if (whenTrue is null || whenFalse is null)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "'whenTrue' and 'whenFalse' must be supplied together"));
            return;
        }

        var trueKind = ClassifyPayload(whenTrue.Value, $"{path}.whenTrue", errors);
        var falseKind = ClassifyPayload(whenFalse.Value, $"{path}.whenFalse", errors);
        if (trueKind is null || falseKind is null)
            return;

        if (trueKind != falseKind)
        {
            errors.Add(new RuleError(path, RuleErrorCode.MixedWhenTrueFalseKinds,
                "'whenTrue' and 'whenFalse' must be the same kind: both strings or both objects"));
            return;
        }

        if (trueKind == JsonValueKind.String)
        {
            node.WhenTrueText = whenTrue.Value.GetString();
            node.WhenFalseText = whenFalse.Value.GetString();
        }
        else
        {
            node.HasObjectPayloads = true;
        }
    }

    private static JsonValueKind? ClassifyPayload(JsonElement element, string path, List<RuleError> errors)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String when string.IsNullOrWhiteSpace(element.GetString()):
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "payload string must not be empty or whitespace"));
                return null;
            case JsonValueKind.String:
                return JsonValueKind.String;
            case JsonValueKind.Object:
                return JsonValueKind.Object;
            default:
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "'whenTrue'/'whenFalse' must be a string or a JSON object"));
                return null;
        }
    }

    private static void ValidateParameterDeclarations(JsonElement element, List<RuleError> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError("$.parameters", RuleErrorCode.InvalidNode,
                "'parameters' must be a JSON object"));
            return;
        }

        foreach (var parameter in element.EnumerateObject())
        {
            var path = $"$.parameters.{parameter.Name}";
            if (parameter.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "parameter declaration must be a JSON object"));
                continue;
            }

            var hasType = false;
            foreach (var property in parameter.Value.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "type":
                        hasType = true;
                        if (property.Value.ValueKind != JsonValueKind.String ||
                            !ParameterTypes.Contains(property.Value.GetString()))
                        {
                            errors.Add(new RuleError($"{path}.type", RuleErrorCode.InvalidNode,
                                "parameter type must be one of 'integer', 'number', 'string' or 'boolean'"));
                        }

                        break;
                    case "default":
                        break;
                    default:
                        errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                            $"unknown property '{property.Name}'"));
                        break;
                }
            }

            if (!hasType)
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "parameter declaration must declare a 'type'"));
        }
    }

    private bool ExceedsLimits(string path, int depth, List<RuleError> errors)
    {
        if (depth > options.MaxDocumentDepth)
            return ReportTooLarge(path, $"document exceeds the maximum depth of {options.MaxDocumentDepth}", errors);

        _nodeCount++;
        if (_nodeCount > options.MaxNodeCount)
            return ReportTooLarge(path, $"document exceeds the maximum node count of {options.MaxNodeCount}",
                errors);

        return false;
    }

    private bool ReportTooLarge(string path, string message, List<RuleError> errors)
    {
        if (!_tooLargeReported)
        {
            _tooLargeReported = true;
            errors.Add(new RuleError(path, RuleErrorCode.DocumentTooLarge, message));
        }

        return true;
    }

    private static string? ReadNonEmptyString(JsonElement element, string path, List<RuleError> errors)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode, "value must be a non-empty string"));
        return null;
    }
}
```

- [ ] **Step 6: Implement RuleSerializer with Validate**

Write `src/Motiv.Serialization/RuleSerializer.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Loads externalized JSON rule documents into Motiv specs, resolving leaf references against a
/// <see cref="SpecRegistry" />.
/// </summary>
public sealed class RuleSerializer
{
    private readonly SpecRegistry _registry;
    private readonly RuleSerializerOptions _options;

    /// <summary>Creates a serializer that resolves spec references against the given registry.</summary>
    /// <param name="registry">The registry used to resolve spec references.</param>
    /// <param name="options">Options controlling validation and loading; defaults are used when omitted.</param>
    public RuleSerializer(SpecRegistry registry, RuleSerializerOptions? options = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? new RuleSerializerOptions();
    }

    /// <summary>
    /// Checks a rule document for structural errors without loading it. Semantic checks that need a
    /// model type (registry lookups, type matching) are not performed.
    /// </summary>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All structural errors found, or an empty list when the document is well-formed.</returns>
    public IReadOnlyList<RuleError> Validate(string json)
    {
        var errors = new List<RuleError>();
        new RuleDocumentParser(_options).Parse(json, errors);
        return errors;
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: PASS (all Task 1 + Task 2 tests).

- [ ] **Step 8: Run all TFMs including net472**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
Expected: PASS on all four TFMs.

- [ ] **Step 9: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: parse and validate rule documents with structured errors"
```

---

### Task 3: Deserialize — registry leaves and decoration

**Files:**
- Create: `src/Motiv.Serialization/RuleBinder.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs` (add `Deserialize<TModel>`)
- Test: `src/Motiv.Serialization.Tests/RuleSerializerDeserializeTests.cs`

**Interfaces:**
- Consumes: `RuleDocumentParser.Parse` and the `RuleNode`/`RuleDocument` shapes (Task 2); `SpecRegistry.Find` (Task 1); from `Motiv`: `SpecBase<TModel>.ToExplanationSpec()` (public, returns `SpecBase<TModel, string>`), `Spec.Build(spec).WhenTrue(string).WhenFalse(string).Create()` / `.Create(string)`, `Spec.Build(spec).Create(string)`.
- Produces: `public SpecBase<TModel, string> Deserialize<TModel>(string json)` on `RuleSerializer`; `internal static class RuleBinder` with `public static SpecBase<TModel, string>? Bind<TModel>(RuleDocument document, SpecRegistry registry, List<RuleError> errors)`. Task 4 extends `RuleBinder` with composition; leaf binding and `Decorate` must keep the exact signatures written here.

- [ ] **Step 1: Write the failing deserialize tests**

Write `src/Motiv.Serialization.Tests/RuleSerializerDeserializeTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class RuleSerializerDeserializeTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, int> HasFlag { get; } =
        Spec.Build((int n) => n != 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("has flag");

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("has-flag", HasFlag));

    private static void ShouldBehaveIdentically<TModel>(
        SpecBase<TModel, string> loaded,
        SpecBase<TModel, string> expected,
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
        }
    }

    [Fact]
    public void Should_load_a_bare_registry_leaf_that_behaves_identically_to_the_fluent_spec()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, IsPositive, 5, -5);
    }

    [Fact]
    public void Should_load_a_decorated_leaf_like_a_fluent_explanation_wrapper()
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
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_load_a_named_decorated_leaf_like_a_named_fluent_wrapper()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad", "name": "check" } }""";
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("ok")
            .WhenFalse("bad")
            .Create("check");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_load_a_name_only_node_like_a_fluent_create_wrapper()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive", "name": "wrapper" } }""";
        var expected = Spec
            .Build(IsPositive)
            .Create("wrapper");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_wrap_the_root_with_the_document_name()
    {
        // Arrange
        const string json = """{ "name": "document rule", "rule": { "spec": "is-positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .Create("document rule");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_degrade_a_non_string_metadata_leaf_to_its_explanation_spec()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "has-flag" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, HasFlag.ToExplanationSpec(), 3, 0);
    }

    [Fact]
    public void Should_decorate_a_non_string_metadata_leaf_with_explanation_strings()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "has-flag", "whenTrue": "on", "whenFalse": "off" } }""";
        var expected = Spec
            .Build(HasFlag)
            .WhenTrue("on")
            .WhenFalse("off")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        // Justification is intentionally not compared here: the loader decorates the entry's
        // explanation view rather than the original generic spec, which may differ in the
        // underlying layers while agreeing on the outcome, reason, and assertions.
        foreach (var model in new[] { 3, 0 })
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
        }
    }

    [Fact]
    public void Should_throw_for_an_unknown_spec_name()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "missing" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("missing");
    }

    [Fact]
    public void Should_throw_for_a_model_type_mismatch()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<string>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
        error.Message.ShouldContain("Int32");
        error.Message.ShouldContain("String");
    }

    [Fact]
    public void Should_throw_when_a_sync_load_references_an_async_spec()
    {
        // Arrange
        var isReadyAsync = Spec
            .BuildAsync((int n) => Task.FromResult(n > 0))
            .Create("is ready");
        var registry = new SpecRegistry().Register("is-ready", isReadyAsync);
        var serializer = new RuleSerializer(registry);
        const string json = """{ "rule": { "spec": "is-ready" } }""";

        // Act
        var act = () => serializer.Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
        error.Message.ShouldContain("is-ready");
    }

    [Fact]
    public void Should_throw_for_an_expression_node_when_expressions_are_not_enabled()
    {
        // Arrange
        const string json = """{ "rule": { "expression": "Age >= 18" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ExpressionsNotEnabled);
        error.Message.ShouldContain("Motiv.Serialization.Expressions");
    }

    [Fact]
    public void Should_throw_for_object_payloads_in_an_explanation_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_throw_structural_errors_from_deserialize()
    {
        // Arrange
        const string json = """{ "rule": { "name": "no operator" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.InvalidNode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: build FAILS with CS1061 (`Deserialize` not on `RuleSerializer`).

- [ ] **Step 3: Implement RuleBinder (leaves + decoration) and Deserialize**

Write `src/Motiv.Serialization/RuleBinder.cs`:

```csharp
namespace Motiv.Serialization;

internal static class RuleBinder
{
    public static SpecBase<TModel, string>? Bind<TModel>(
        RuleDocument document,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, registry, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    private static SpecBase<TModel, string>? BindNode<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var spec = node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, registry, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], registry, errors)?.Not(),
            _ => throw new NotSupportedException($"Unsupported rule operator '{node.Operator}'.")
        };

        return spec is null ? null : Decorate(node, spec, errors);
    }

    private static SpecBase<TModel, string>? BindSpecLeaf<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
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

        return entry.MetadataType == typeof(string)
            ? (SpecBase<TModel, string>)spec
            : spec.ToExplanationSpec();
    }

    private static SpecBase<TModel, string>? BindExpressionLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private static SpecBase<TModel, string>? Decorate<TModel>(
        RuleNode node,
        SpecBase<TModel, string> spec,
        List<RuleError> errors)
    {
        if (node.HasObjectPayloads)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
                "object 'whenTrue'/'whenFalse' payloads require a metadata load; this is an explanation load"));
            return null;
        }

        if (node.WhenTrueText is not null)
        {
            var builder = Spec.Build(spec).WhenTrue(node.WhenTrueText).WhenFalse(node.WhenFalseText!);
            return node.Name is null ? builder.Create() : builder.Create(node.Name);
        }

        return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
    }
}
```

Add to `src/Motiv.Serialization/RuleSerializer.cs` (below `Validate`):

```csharp
    /// <summary>
    /// Loads a rule document into an explanation spec, resolving spec references against the
    /// registry. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, string> Deserialize<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(_options).Parse(json, errors);
        ThrowIfInvalid(errors);

        var spec = RuleBinder.Bind<TModel>(document!, _registry, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    private static void ThrowIfInvalid(List<RuleError> errors)
    {
        if (errors.Count > 0)
            throw new RuleSerializationException(errors);
    }
```

Note: the `_ => throw new NotSupportedException(...)` arm in `BindNode` is temporary — Task 4 replaces it with composition binding. Composition-node tests are deliberately absent from this task.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: PASS. If `Should_load_a_bare_registry_leaf...` fails on `Reason`/`Justification`, the exact-cast branch for string-metadata entries is missing — the leaf must be returned as-is, not adapted.

- [ ] **Step 5: Run all TFMs including net472**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
Expected: PASS on all four TFMs.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: deserialize rule documents into explanation specs"
```

---

### Task 4: Composition operators

**Files:**
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (replace the `NotSupportedException` arm with composition binding)
- Test: `src/Motiv.Serialization.Tests/RuleCompositionTests.cs`

**Interfaces:**
- Consumes: `RuleBinder.BindNode<TModel>` and `RuleNode.Children` (Tasks 2–3); from `Motiv`: `SpecBase<TModel, string>.And/Or/XOr/AndAlso/OrElse(SpecBase<TModel, string>)` and `.Not()`.
- Produces: `and`/`or`/`xor`/`andAlso`/`orElse`/`not` nodes bind to the corresponding Motiv operators; arrays of more than two nodes fold left.

- [ ] **Step 1: Write the failing composition tests**

Write `src/Motiv.Serialization.Tests/RuleCompositionTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class RuleCompositionTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).Create("is positive");

    private static SpecBase<int, string> IsEven { get; } =
        Spec.Build((int n) => n % 2 == 0).Create("is even");

    private static SpecBase<int, string> IsBig { get; } =
        Spec.Build((int n) => Math.Abs(n) > 100).Create("is big");

    private static SpecBase<int, string> Throws { get; } =
        Spec.Build((int _) => (bool)ThrowBoom()).Create("throws");

    private static object ThrowBoom() => throw new InvalidOperationException("boom");

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("is-even", IsEven)
            .Register("is-big", IsBig)
            .Register("throws", Throws));

    private static readonly int[] Models = [2, 3, -2, -3];

    private static void ShouldBehaveIdentically(
        SpecBase<int, string> loaded,
        SpecBase<int, string> expected)
    {
        foreach (var model in Models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
        }
    }

    public static TheoryData<string, string> BinaryOperators => new()
    {
        { "and", "And" },
        { "or", "Or" },
        { "xor", "XOr" },
        { "andAlso", "AndAlso" },
        { "orElse", "OrElse" }
    };

    private static SpecBase<int, string> Compose(
        string method,
        SpecBase<int, string> left,
        SpecBase<int, string> right) =>
        method switch
        {
            "And" => left.And(right),
            "Or" => left.Or(right),
            "XOr" => left.XOr(right),
            "AndAlso" => left.AndAlso(right),
            _ => left.OrElse(right)
        };

    [Theory]
    [MemberData(nameof(BinaryOperators))]
    public void Should_bind_each_binary_operator_to_its_fluent_equivalent(string jsonOperator, string method)
    {
        // Arrange
        var json =
            $$"""{ "rule": { "{{jsonOperator}}": [ { "spec": "is-positive" }, { "spec": "is-even" } ] } }""";
        var expected = Compose(method, IsPositive, IsEven);

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_bind_not_to_the_fluent_negation()
    {
        // Arrange
        const string json = """{ "rule": { "not": { "spec": "is-positive" } } }""";
        var expected = IsPositive.Not();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_fold_arrays_of_more_than_two_operands_to_the_left()
    {
        // Arrange
        const string json =
            """
            { "rule": { "and": [ { "spec": "is-positive" }, { "spec": "is-even" }, { "spec": "is-big" } ] } }
            """;
        var expected = IsPositive.And(IsEven).And(IsBig);

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_short_circuit_andAlso_like_the_fluent_operator()
    {
        // Arrange
        const string json =
            """{ "rule": { "andAlso": [ { "spec": "is-positive" }, { "spec": "throws" } ] } }""";
        var expected = IsPositive.AndAlso(Throws);

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert: -5 fails the left operand, so the throwing right operand is never evaluated
        var expectedResult = expected.Evaluate(-5);
        var actualResult = loaded.Evaluate(-5);
        actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
        actualResult.Reason.ShouldBe(expectedResult.Reason);
    }

    [Fact]
    public void Should_decorate_a_composition_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "or": [ { "spec": "is-positive" }, { "spec": "is-even" } ],
                "whenTrue": "acceptable",
                "whenFalse": "unacceptable",
                "name": "acceptability"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive.Or(IsEven))
            .WhenTrue("acceptable")
            .WhenFalse("unacceptable")
            .Create("acceptability");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_load_a_nested_document_identically_to_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "name": "eligibility",
              "rule": {
                "andAlso": [
                  { "spec": "is-positive" },
                  { "not": { "spec": "is-big" } },
                  {
                    "or": [ { "spec": "is-even" }, { "spec": "is-big" } ],
                    "whenTrue": "shape is fine",
                    "whenFalse": "shape is wrong"
                  }
                ]
              }
            }
            """;
        var inner = Spec
            .Build(IsEven.Or(IsBig))
            .WhenTrue("shape is fine")
            .WhenFalse("shape is wrong")
            .Create();
        var expected = Spec
            .Build(IsPositive.AndAlso(IsBig.Not()).AndAlso(inner))
            .Create("eligibility");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_collect_errors_from_every_operand()
    {
        // Arrange
        const string json =
            """{ "rule": { "and": [ { "spec": "missing-1" }, { "spec": "missing-2" } ] } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldAllBe(error => error.Code == RuleErrorCode.UnknownSpec);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: composition tests FAIL with `NotSupportedException: Unsupported rule operator 'And'` (and siblings).

- [ ] **Step 3: Implement composition binding**

In `src/Motiv.Serialization/RuleBinder.cs`, replace the `BindNode<TModel>` switch arm

```csharp
            _ => throw new NotSupportedException($"Unsupported rule operator '{node.Operator}'.")
```

with

```csharp
            _ => BindComposition<TModel>(node, registry, errors)
```

and add the method:

```csharp
    private static SpecBase<TModel, string>? BindComposition<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var children = node.Children
            .Select(child => BindNode<TModel>(child, registry, errors))
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: PASS (all tests in the project).

- [ ] **Step 5: Run all TFMs including net472**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
Expected: PASS on all four TFMs.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: bind rule-document composition operators to Motiv operators"
```

---

### Task 5: JSON Schema and schema tests

**Files:**
- Create: `schemas/rule.v1.json`
- Modify: `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj` (JsonSchema.Net + schema content link)
- Test: `src/Motiv.Serialization.Tests/RuleSchemaTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks — the schema is a standalone format contract.
- Produces: `schemas/rule.v1.json`, the schema the design doc's `$schema` URL points at. It covers the FULL v1 format (including parameters, expressions, and higher-order nodes from Plans 2–3), not just what this plan's loader accepts.

- [ ] **Step 1: Add the JsonSchema.Net test dependency**

Run:

```bash
dotnet add src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj package JsonSchema.Net
```

(With central package management the version lands in `Directory.Packages.props` automatically; if the CLI errors on CPM, add `<PackageReference Include="JsonSchema.Net" />` to the csproj and `<PackageVersion Include="JsonSchema.Net" Version="X.Y.Z" />` — using the latest version from `dotnet package search JsonSchema.Net --exact-match` — to `Directory.Packages.props` by hand.)

Add to the test csproj:

```xml
<ItemGroup>
    <Content Include="..\..\schemas\rule.v1.json" Link="schemas\rule.v1.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**net472 contingency:** if restore or the net472 test run fails because the chosen JsonSchema.Net version does not support .NET Framework, (a) condition the reference with `<ItemGroup Condition="'$(TargetFramework)' != 'net472'">` and (b) wrap the whole `RuleSchemaTests.cs` file in `#if !NET472` / `#endif`.

- [ ] **Step 2: Write the failing schema tests**

Write `src/Motiv.Serialization.Tests/RuleSchemaTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Json.Schema;

namespace Motiv.Serialization.Tests;

public class RuleSchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(
        Path.Combine(AppContext.BaseDirectory, "schemas", "rule.v1.json"));

    private static bool IsValid(string json) =>
        Schema.Evaluate(JsonNode.Parse(json)).IsValid;

    public static TheoryData<string> ValidDocuments => new()
    {
        """{ "rule": { "spec": "is-positive" } }""",
        """{ "$schema": "https://example.com/rule.v1.json", "name": "doc", "rule": { "spec": "a" } }""",
        """{ "rule": { "and": [ { "spec": "a" }, { "not": { "spec": "b" } } ] } }""",
        """{ "rule": { "or": [ { "spec": "a" }, { "spec": "b" }, { "spec": "c" } ] } }""",
        """{ "rule": { "xor": [ { "spec": "a" }, { "spec": "b" } ] } }""",
        """{ "rule": { "andAlso": [ { "spec": "a" }, { "spec": "b" } ] } }""",
        """{ "rule": { "orElse": [ { "spec": "a" }, { "spec": "b" } ] } }""",
        """{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": "no", "name": "n" } }""",
        """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""",
        """{ "rule": { "expression": "Age >= @minAge" } }""",
        """{ "parameters": { "minAge": { "type": "integer", "default": 18 } }, "rule": { "spec": "a" } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "Orders" } }""",
        """{ "rule": { "asAnySatisfied": { "spec": "a" } } }""",
        """{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 3 } }""",
        """{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": "@minOrders", "path": "Account.Orders" } }""",
        """{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 2 } }"""
    };

    [Theory]
    [MemberData(nameof(ValidDocuments))]
    public void Should_accept_valid_documents(string json)
    {
        IsValid(json).ShouldBeTrue();
    }

    public static TheoryData<string> InvalidDocuments => new()
    {
        """{ }""",
        """{ "rule": { } }""",
        """{ "frobnicate": 1, "rule": { "spec": "a" } }""",
        """{ "rule": { "spec": "a", "frobnicate": true } }""",
        """{ "rule": { "spec": "" } }""",
        """{ "rule": { "spec": "a", "expression": "Age >= 18" } }""",
        """{ "rule": { "and": [ { "spec": "a" } ] } }""",
        """{ "rule": { "not": [ { "spec": "a" } ] } }""",
        """{ "rule": { "spec": "a", "whenTrue": "yes" } }""",
        """{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": { "code": 2 } } }""",
        """{ "rule": { "spec": "a", "whenTrue": 1, "whenFalse": 2 } }""",
        """{ "parameters": { "minAge": { "default": 18 } }, "rule": { "spec": "a" } }""",
        """{ "parameters": { "minAge": { "type": "decimal" } }, "rule": { "spec": "a" } }""",
        """{ "rule": { "asNSatisfied": { "spec": "a" } } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": 3 } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "Orders..Items" } }""",
        """{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "minOrders" } }"""
    };

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void Should_reject_invalid_documents(string json)
    {
        IsValid(json).ShouldBeFalse();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: FAIL — `rule.v1.json` does not exist yet (FileNotFoundException).

- [ ] **Step 4: Write the schema**

Write `schemas/rule.v1.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://raw.githubusercontent.com/karlssberg/Motiv/main/schemas/rule.v1.json",
  "title": "Motiv rule document",
  "description": "An externalized Motiv rule: a tree of registry references, expressions, and logical composition, with optional explanation or metadata payloads. See https://karlssberg.github.io/Motiv",
  "type": "object",
  "properties": {
    "$schema": { "type": "string" },
    "name": { "$ref": "#/$defs/nonEmptyString" },
    "parameters": {
      "type": "object",
      "additionalProperties": { "$ref": "#/$defs/parameterDeclaration" }
    },
    "rule": { "$ref": "#/$defs/node" }
  },
  "required": ["rule"],
  "additionalProperties": false,
  "$defs": {
    "nonEmptyString": { "type": "string", "pattern": "\\S" },
    "parameterDeclaration": {
      "type": "object",
      "properties": {
        "type": { "enum": ["integer", "number", "string", "boolean"] },
        "default": { "type": ["integer", "number", "string", "boolean"] }
      },
      "required": ["type"],
      "additionalProperties": false
    },
    "payload": {
      "anyOf": [{ "$ref": "#/$defs/nonEmptyString" }, { "type": "object" }]
    },
    "countable": {
      "anyOf": [
        { "type": "integer", "minimum": 0 },
        { "type": "string", "pattern": "^@[A-Za-z_][A-Za-z0-9_]*$" }
      ]
    },
    "propertyPath": {
      "type": "string",
      "pattern": "^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$"
    },
    "nodeArray": {
      "type": "array",
      "items": { "$ref": "#/$defs/node" },
      "minItems": 2
    },
    "sameKindPayloads": {
      "dependentRequired": {
        "whenTrue": ["whenFalse"],
        "whenFalse": ["whenTrue"]
      },
      "anyOf": [
        { "not": { "required": ["whenTrue"] } },
        {
          "properties": {
            "whenTrue": { "type": "string" },
            "whenFalse": { "type": "string" }
          }
        },
        {
          "properties": {
            "whenTrue": { "type": "object" },
            "whenFalse": { "type": "object" }
          }
        }
      ]
    },
    "node": {
      "type": "object",
      "allOf": [{ "$ref": "#/$defs/sameKindPayloads" }],
      "oneOf": [
        { "$ref": "#/$defs/specNode" },
        { "$ref": "#/$defs/expressionNode" },
        { "$ref": "#/$defs/notNode" },
        { "$ref": "#/$defs/andNode" },
        { "$ref": "#/$defs/orNode" },
        { "$ref": "#/$defs/xorNode" },
        { "$ref": "#/$defs/andAlsoNode" },
        { "$ref": "#/$defs/orElseNode" },
        { "$ref": "#/$defs/asAllSatisfiedNode" },
        { "$ref": "#/$defs/asAnySatisfiedNode" },
        { "$ref": "#/$defs/asNSatisfiedNode" },
        { "$ref": "#/$defs/asAtLeastNSatisfiedNode" },
        { "$ref": "#/$defs/asAtMostNSatisfiedNode" }
      ]
    },
    "specNode": {
      "properties": {
        "spec": { "$ref": "#/$defs/nonEmptyString" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["spec"],
      "additionalProperties": false
    },
    "expressionNode": {
      "properties": {
        "expression": { "$ref": "#/$defs/nonEmptyString" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["expression"],
      "additionalProperties": false
    },
    "notNode": {
      "properties": {
        "not": { "$ref": "#/$defs/node" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["not"],
      "additionalProperties": false
    },
    "andNode": {
      "properties": {
        "and": { "$ref": "#/$defs/nodeArray" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["and"],
      "additionalProperties": false
    },
    "orNode": {
      "properties": {
        "or": { "$ref": "#/$defs/nodeArray" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["or"],
      "additionalProperties": false
    },
    "xorNode": {
      "properties": {
        "xor": { "$ref": "#/$defs/nodeArray" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["xor"],
      "additionalProperties": false
    },
    "andAlsoNode": {
      "properties": {
        "andAlso": { "$ref": "#/$defs/nodeArray" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["andAlso"],
      "additionalProperties": false
    },
    "orElseNode": {
      "properties": {
        "orElse": { "$ref": "#/$defs/nodeArray" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["orElse"],
      "additionalProperties": false
    },
    "asAllSatisfiedNode": {
      "properties": {
        "asAllSatisfied": { "$ref": "#/$defs/node" },
        "path": { "$ref": "#/$defs/propertyPath" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["asAllSatisfied"],
      "additionalProperties": false
    },
    "asAnySatisfiedNode": {
      "properties": {
        "asAnySatisfied": { "$ref": "#/$defs/node" },
        "path": { "$ref": "#/$defs/propertyPath" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["asAnySatisfied"],
      "additionalProperties": false
    },
    "asNSatisfiedNode": {
      "properties": {
        "asNSatisfied": { "$ref": "#/$defs/node" },
        "n": { "$ref": "#/$defs/countable" },
        "path": { "$ref": "#/$defs/propertyPath" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["asNSatisfied", "n"],
      "additionalProperties": false
    },
    "asAtLeastNSatisfiedNode": {
      "properties": {
        "asAtLeastNSatisfied": { "$ref": "#/$defs/node" },
        "n": { "$ref": "#/$defs/countable" },
        "path": { "$ref": "#/$defs/propertyPath" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["asAtLeastNSatisfied", "n"],
      "additionalProperties": false
    },
    "asAtMostNSatisfiedNode": {
      "properties": {
        "asAtMostNSatisfied": { "$ref": "#/$defs/node" },
        "n": { "$ref": "#/$defs/countable" },
        "path": { "$ref": "#/$defs/propertyPath" },
        "whenTrue": { "$ref": "#/$defs/payload" },
        "whenFalse": { "$ref": "#/$defs/payload" },
        "name": { "$ref": "#/$defs/nonEmptyString" }
      },
      "required": ["asAtMostNSatisfied", "n"],
      "additionalProperties": false
    }
  }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0`
Expected: PASS. If a *valid* document is rejected, debug by printing `Schema.Evaluate(JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List })` details — the usual culprit is a `oneOf` variant missing a decoration property while `additionalProperties: false` is set.

- [ ] **Step 6: Run all TFMs including net472**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
Expected: PASS on all four TFMs (or net472 skipped per the Step 1 contingency).

- [ ] **Step 7: Commit**

```bash
git add schemas src/Motiv.Serialization.Tests Directory.Packages.props
git commit -m "feat: add the rule.v1 JSON Schema with conformance tests"
```

---

### Task 6: Full-suite verification

**Files:** none (verification only).

**Interfaces:**
- Consumes: everything above.
- Produces: a green solution — the gate for calling Plan 1 complete.

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build Motiv.slnx`
Expected: 0 errors, 0 warnings (warnings are errors solution-wide).

- [ ] **Step 2: Run the full solution test suite**

Run: `dotnet test Motiv.slnx`
Expected: all projects PASS on all their TFMs (~13k tests plus the new Motiv.Serialization tests).

- [ ] **Step 3: Explicit net472 run of the new tests**

Run: `dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net472`
Expected: PASS. (Known gotcha: net10-only runs hide netfx breaks — this run is mandatory.)

- [ ] **Step 4: Verify packaging**

Run: `dotnet pack src/Motiv.Serialization/Motiv.Serialization.csproj -c Release -o artifacts-tmp && ls artifacts-tmp`
Expected: `Motiv.Serialization.<version>.nupkg` (and `.snupkg`) produced. Then delete the scratch dir: `rm -rf artifacts-tmp`.

- [ ] **Step 5: Commit any stragglers and wrap up**

```bash
git status
```

Expected: clean tree (everything committed in Tasks 1–5). If files remain, commit them with an appropriate message.

Post-implementation (per project convention): spawn a `code-simplifier` agent over the changed code, apply its worthwhile findings, re-run the affected tests.
