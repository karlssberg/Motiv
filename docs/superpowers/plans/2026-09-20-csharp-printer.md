# C# Printer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A rule document prints as the C# builder chain that would compile to the same specification, so a reproduced decision (or any stored rule) can be adopted as code; a fidelity test over a corpus proves the printed C# decides and explains as the document does.

**Architecture:** `CSharpPrinter` in `Motiv.Serialization` walks the parsed `RuleDocument` and emits a static method returning `SpecBase<TModel, string>` — definitions as `var` locals, parameters as method arguments, spec references through a caller-supplied handle map or `registry.Get<TModel>("name")`, higher-order nodes as the exact `Spec.Build(...).AsAllSatisfied()...` chain the binder builds, decorations as `.WhenTrue/.WhenFalse/.Create`. `SpecRegistry.Get<TModel>` is the one new public runtime method the printed code needs. `Reproduction` gains `CSharp`. The fidelity test compiles each corpus document's print with Roslyn and evaluates both over a scenario set.

**Tech Stack:** C# / .NET 10 (`Motiv.Serialization` also targets net8, net9, netstandard2.0), `Microsoft.CodeAnalysis.CSharp` 5.0.0 (already in `Directory.Packages.props`) in the test project only, xUnit + Shouldly.

**Spec:** `docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md`, section 6. Slice 4 of 5. Slice 5 adds the endpoints, the MCP and the testing package.

## Global Constraints

- `Motiv.Serialization` targets `net8.0;net9.0;netstandard2.0;net10.0`: no `Index`/`Range`, no default interface methods, `string.Substring` not ranges, `CultureInfo.InvariantCulture` for every number printed.
- `Motiv.Serialization.Tests` also targets `net472`, which CI builds but never runs: the Roslyn fidelity harness is guarded `#if !NETFRAMEWORK` (no `TRUSTED_PLATFORM_ASSEMBLIES` there), and its package reference must still restore on net472 (Roslyn 5.0 is netstandard2.0; fine).
- Every `dotnet` call needs `env -u MallocStackLogging -u MallocNanoZone` and the sandbox disabled; grep for `error CS`.
- The printer never guesses `TModel`: `CSharpPrintOptions.ModelType` is required and comes from the rule's `ModelType` or the registry entry's.
- The printer mirrors the binder exactly where assertions are concerned: higher-order `WhenTrue`/`WhenFalse` texts are `HigherOrder.Build`'s (`"all satisfied"` / `"not all satisfied"`, `"any satisfied"` / `"none satisfied"`, `"exactly {n} satisfied"` / `"not exactly {n} satisfied"`, `"at least {n} satisfied"` / `"fewer than {n} satisfied"`, `"at most {n} satisfied"` / `"more than {n} satisfied"`); n-ary compositions fold left as `BindComposition` does.
- Branch: `claude/csharp-printer`, stacked on `claude/decision-reproducer`. Plan and design doc (`docs/superpowers/specs/2026-09-20-csharp-printer-design.md`) land on the branch before the PR opens.

## Review Focus

1. A document whose `whenTrue` text contains `{param}` must print as an interpolated string that yields the same assertion the substituter produces at bind time, including `{{` escapes. Pinned by corpus document `10-parameters-with-n.json` (Task 3) and `Should_print_parameter_interpolation_as_an_interpolated_string` (Task 2).
2. An `n-ary` `and` (three or more operands) must fold left exactly as the binder does, or `Justification` shapes differ. Pinned by `14-n-ary-and.json` (Task 3).
3. A definition key that is not a C# identifier (`is-active`, `2fa`) must become a valid, unique local name. Pinned by `Should_turn_definition_keys_into_valid_unique_identifiers` (Task 2).
4. A higher-order node whose collection has no handle must still print compilable-shaped code with a `TODO` and a warning, never throw. Pinned by `Should_print_a_higher_order_node_without_a_collection_handle_as_a_todo` (Task 2).
5. Object `whenTrue`/`whenFalse` payloads must print as commented string stand-ins with a warning, since the printer prints explanation rules. Pinned by `Should_print_object_payloads_as_todo_strings` (Task 2).

---

### Task 1: `SpecRegistry.Get<TModel>` — the handle printed code resolves through

**Files:**
- Modify: `src/Motiv.Serialization/SpecRegistry.cs`
- Modify: `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`

**Interfaces:**
- Produces: `public SpecBase<TModel, string> Get<TModel>(string name, IReadOnlyDictionary<string, object?>? args = null)` and `public AsyncSpecBase<TModel, string> GetAsync<TModel>(string name, IReadOnlyDictionary<string, object?>? args = null)` on `SpecRegistry`. Unknown name, wrong model type, async/sync mismatch, or argument errors throw `RuleSerializationException` carrying the `RuleError`s the binder would report. A non-string metadata entry is returned through `ToExplanationSpec()` as `BindSpecLeaf` does.

- [ ] **Step 1: Tests**

Append to `SpecRegistryTests`:

```csharp
    [Fact]
    public void Should_get_a_registered_spec_by_name_as_an_explanation_spec()
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);

        var spec = registry.Get<Customer>("customer.is-active");

        spec.Evaluate(new Customer(true)).Assertions.ShouldBe(["customer is active"]);
    }

    [Fact]
    public void Should_get_a_parameterised_spec_with_arguments()
    {
        var registry = new SpecRegistry().RegisterParameterised<Customer>(
            "customer.older-than",
            [new RuleParameterDeclaration("age", RuleParameterType.Integer, false, null)],
            args => Spec.Build((Customer c) => c.Age > (int)args["age"]!).Create($"older than {args["age"]}"));

        var spec = registry.Get<Customer>("customer.older-than", new Dictionary<string, object?> { ["age"] = 18 });

        spec.Evaluate(new Customer(true, Age: 30)).Reason.ShouldBe("older than 18 == true");
    }

    [Fact]
    public void Should_refuse_to_get_an_unknown_name_a_wrong_model_or_an_async_spec_synchronously()
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive).Register("customer.is-active-async", IsActiveAsync);

        Should.Throw<RuleSerializationException>(() => registry.Get<Customer>("missing")).Errors.ShouldContain(e => e.Code == RuleErrorCode.UnknownSpec);
        Should.Throw<RuleSerializationException>(() => registry.Get<int>("customer.is-active")).Errors.ShouldContain(e => e.Code == RuleErrorCode.ModelTypeMismatch);
        Should.Throw<RuleSerializationException>(() => registry.Get<Customer>("customer.is-active-async")).Errors.ShouldContain(e => e.Code == RuleErrorCode.AsyncSpecInSyncLoad);
        registry.GetAsync<Customer>("customer.is-active-async").ShouldNotBeNull();
    }
```

Read the file's existing `Customer` record and fixtures first; add `Age` to the record and an `IsActiveAsync` fixture (`Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.IsActive)).WhenTrue("active").WhenFalse("inactive").Create()`) if absent.

- [ ] **Step 2: Run red**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~SpecRegistryTests" 2>&1 | grep -E "error CS" | sed -E 's/.*(error CS[0-9]+: [^[]*).*/\1/' | sort -u | head -3
```

Expected: `Get`/`GetAsync` not found.

- [ ] **Step 3: Implement**

In `SpecRegistry`, beside `Find`:

```csharp
    /// <summary>
    /// The spec registered under <paramref name="name"/>, as the explanation spec a rule document's
    /// <c>spec</c> node binds to. What printed C# resolves an unmapped reference through.
    /// </summary>
    /// <exception cref="RuleSerializationException">The name is unknown, is an async spec, has another model type, or the arguments do not match its declaration.</exception>
    public SpecBase<TModel, string> Get<TModel>(string name, IReadOnlyDictionary<string, object?>? args = null)
    {
        var errors = new List<RuleError>();
        var node = new RuleNode(RuleOperator.Spec, "$") { SpecName = name, Args = args?.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal) };
        var spec = RuleBinder.BindOperator<TModel>(node, this, errors);
        return spec ?? throw new RuleSerializationException(errors);
    }

    /// <summary><see cref="Get{TModel}"/> for an async spec.</summary>
    public AsyncSpecBase<TModel, string> GetAsync<TModel>(string name, IReadOnlyDictionary<string, object?>? args = null)
    {
        var errors = new List<RuleError>();
        var node = new RuleNode(RuleOperator.Spec, "$") { SpecName = name, Args = args?.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal) };
        var spec = AsyncRuleBinder.BindOperator<TModel>(node, this, errors);
        return spec ?? throw new RuleSerializationException(errors);
    }
```

`RuleBinder.BindOperator` is already `public static` on an internal class and handles unknown name, async mismatch, model mismatch, args and the explanation conversion. Read `AsyncRuleBinder.cs` for the async twin's name (grep `BindOperator` there; if it is private, make it `internal`). `RuleNode`'s `Args` is `Dictionary<string, object?>?`.

- [ ] **Step 4: Run green, then the project on net10; commit**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 2>&1 | grep -E "error CS|Passed!|Failed!" | sed -E 's/ \[.*//'
git add src/Motiv.Serialization/SpecRegistry.cs src/Motiv.Serialization.Tests/SpecRegistryTests.cs
git commit -m "SpecRegistry — Get<TModel>(name, args) and GetAsync: the handle printed C# resolves a document reference through

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: `CSharpPrinter`

**Files:**
- Create: `src/Motiv.Serialization/Printing/CSharpPrintOptions.cs` (`CSharpPrintOptions`, `CSharpCollectionHandle`, `CSharpPrintedRule`)
- Create: `src/Motiv.Serialization/Printing/CSharpPrinter.cs`
- Create: `src/Motiv.Serialization.Tests/Printing/CSharpPrinterTests.cs`

**Interfaces:**
- Produces (namespace `Motiv.Serialization`):

```csharp
public sealed class CSharpPrintOptions
{
    public required Type ModelType { get; init; }
    public string RegistryExpression { get; init; } = "registry";
    public IReadOnlyDictionary<string, string> SpecHandles { get; init; }              // spec name -> C# expression
    public IReadOnlyDictionary<string, CSharpCollectionHandle> Collections { get; init; } // path -> handle
    public IReadOnlySet<string> AsyncSpecs { get; init; }                               // names registered async (netstandard2.0: use ISet<string>)
    public string? Namespace { get; init; }
    public string? ClassName { get; init; }
    public string MethodName { get; init; } = "Build";
    public RuleSerializerOptions? SerializerOptions { get; init; }
}
public sealed record CSharpCollectionHandle(string ElementType, string? Selector);   // ("int", "c => c.Orders")
public sealed record CSharpPrintedRule(string Source, IReadOnlyList<string> Warnings);
public static class CSharpPrinter
{
    public static CSharpPrintedRule Print(string documentJson, CSharpPrintOptions options);  // throws RuleSerializationException when the document does not parse
    internal static CSharpPrintedRule Print(RuleDocument document, CSharpPrintOptions options);
}
```

**Output contract** (the tests below pin each line):

- Class mode (`ClassName` set): `using Motiv;`, `using Motiv.Serialization;`, `using System.Collections.Generic;` (only when an args dictionary is printed), `using <ModelType.Namespace>;` when the model has one; `namespace X;` when set; `public static class <ClassName>` wrapping the method. Snippet mode (`ClassName` null): the method alone, no usings.
- Method: `public static SpecBase<TModel, string> <MethodName>(SpecRegistry <RegistryExpression>[, <params>])`. Return type is `AsyncSpecBase<TModel, string>` when any referenced spec name is in `AsyncSpecs`. Parameters: required ones first in declaration order, then defaulted; `Integer`→`int`, `Number`→`double`, `String`→`string`, `Boolean`→`bool`; defaults as literals (`InvariantCulture`; doubles with `d` suffix, e.g. `1.5d`, `2d`).
- Body: one `var <local> = <expr>;` per definition in declaration order, then `return <root expr>;` where a named document wraps as `Spec.Build(<root>).Create("<name>")`.
- Node expressions:
  - Spec: `SpecHandles[name]` if present, else `<registry>.Get<TModel>("name")` / `GetAsync` for async names; with args: `<registry>.Get<TModel>("name", new Dictionary<string, object?> { ["p"] = 3 })`. Scalar literals: `int` → `3`, `long` → `3L`, `double` → `2.5d`, `string` → `"..."` (escape `\` `"` `\n` `\r` `\t`), `bool` → `true`, `null` → `null`.
  - Local: the definition's identifier: camelCase of the key with non-identifier characters replaced by `_`, `_` prefixed when it starts with a digit, `@` prefixed when it is a C# keyword, a numeric suffix when already taken.
  - Expression: `Spec.From((TModel m) => <text>) /* expression printed verbatim; not re-parsed */` and warning `"$.rule...: expression leaf printed verbatim"`.
  - Not: `!<operand>`. And/Or/XOr: `(<a> & <b>)`, `(<a> | <b>)`, `(<a> ^ <b>)`, folded left for n operands: `((a & b) & c)`. AndAlso/OrElse: `<a>.AndAlso(<b>)`, folded left.
  - Higher-order: `Spec.Build(<inner over TElement>).AsAllSatisfied().WhenTrue("all satisfied").WhenFalse("not all satisfied").Create().ChangeModelTo<TModel>(<selector>)`; `AsNSatisfied(<n>)` etc. with the texts above as literals, or as `$"exactly {p} satisfied"` when `n` is `@p`. `TElement` and the selector come from `Collections[path]`; without a handle: element type `object`, selector `m => m.<PascalCase(path)> /* TODO: the collection registered at 'path' */`, and a warning.
  - Decoration (any node with `whenTrue`): `Spec.Build(<x>).WhenTrue("t").WhenFalse("f").Create()` or `.Create("name")`; name only: `Spec.Build(<x>).Create("name")`. A text containing `{` or `}` prints as `$"..."` verbatim (the document's `{p}` and `{{` are already C# interpolation syntax). Object payloads: `.WhenTrue("<compact json>") /* TODO: object payload */` and a warning.
  - Every binary composition result is parenthesised, so `!` and `.AndAlso` apply without ambiguity.

- [ ] **Step 1: Tests**

`src/Motiv.Serialization.Tests/Printing/CSharpPrinterTests.cs`:

```csharp
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Printing;

public class CSharpPrinterTests
{
    public sealed record Customer(bool IsActive, int Age, IReadOnlyList<int> Orders);

    private static CSharpPrintOptions Options(Action<CSharpPrintOptionsBuilder>? configure = null)
    {
        var b = new CSharpPrintOptionsBuilder();
        configure?.Invoke(b);
        return b.Build();
    }

    private sealed class CSharpPrintOptionsBuilder
    {
        public Dictionary<string, string> Handles { get; } = new();
        public Dictionary<string, CSharpCollectionHandle> Collections { get; } = new();
        public HashSet<string> Async { get; } = new();
        public string? ClassName { get; set; }
        public string? Namespace { get; set; }
        public CSharpPrintOptions Build() => new()
        {
            ModelType = typeof(Customer), SpecHandles = Handles, Collections = Collections, AsyncSpecs = Async,
            ClassName = ClassName, Namespace = Namespace,
        };
    }

    private static string Body(string json, Action<CSharpPrintOptionsBuilder>? configure = null) =>
        CSharpPrinter.Print(json, Options(configure)).Source;

    [Fact]
    public void Should_print_a_spec_leaf_through_the_registry_when_no_handle_is_supplied()
    {
        var source = Body("""{ "rule": { "spec": "customer.is-active" } }""");

        source.ShouldBe("""
            public static SpecBase<Customer, string> Build(SpecRegistry registry)
            {
                return registry.Get<Customer>("customer.is-active");
            }
            """.ReplaceLineEndings("\n").TrimEnd() + "\n");
    }

    [Fact]
    public void Should_print_a_spec_leaf_through_its_handle_and_a_named_root_as_create()
    {
        var source = Body("""{ "name": "eligible", "rule": { "spec": "customer.is-active" } }""",
            o => o.Handles["customer.is-active"] = "Specs.IsActive");

        source.ShouldContain("return Spec.Build(Specs.IsActive).Create(\"eligible\");");
    }

    [Fact]
    public void Should_fold_compositions_left_and_parenthesise_them()
    {
        var source = Body("""{ "rule": { "and": [ { "spec": "a" }, { "spec": "b" }, { "not": { "or": [ { "spec": "c" }, { "spec": "d" } ] } } ] } }""",
            o => { o.Handles["a"] = "A"; o.Handles["b"] = "B"; o.Handles["c"] = "C"; o.Handles["d"] = "D"; });

        source.ShouldContain("return ((A & B) & !(C | D));");
    }

    [Fact]
    public void Should_print_short_circuit_operators_as_method_calls()
    {
        var source = Body("""{ "rule": { "orElse": [ { "andAlso": [ { "spec": "a" }, { "spec": "b" } ] }, { "xor": [ { "spec": "c" }, { "spec": "d" } ] } ] } }""",
            o => { o.Handles["a"] = "A"; o.Handles["b"] = "B"; o.Handles["c"] = "C"; o.Handles["d"] = "D"; });

        source.ShouldContain("return A.AndAlso(B).OrElse((C ^ D));");
    }

    [Fact]
    public void Should_print_decorations_as_the_builder_chain()
    {
        var unnamed = Body("""{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": "no" } }""", o => o.Handles["a"] = "A");
        var named = Body("""{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": "no", "name": "the answer" } }""", o => o.Handles["a"] = "A");
        var nameOnly = Body("""{ "rule": { "spec": "a", "name": "the answer" } }""", o => o.Handles["a"] = "A");

        unnamed.ShouldContain("""return Spec.Build(A).WhenTrue("yes").WhenFalse("no").Create();""");
        named.ShouldContain("""return Spec.Build(A).WhenTrue("yes").WhenFalse("no").Create("the answer");""");
        nameOnly.ShouldContain("""return Spec.Build(A).Create("the answer");""");
    }

    [Fact]
    public void Should_turn_definition_keys_into_valid_unique_identifiers()
    {
        var source = Body("""
            { "definitions": {
                "is-active": { "rule": { "spec": "a" } },
                "is active": { "rule": { "spec": "b" } },
                "2fa": { "rule": { "spec": "c" } },
                "class": { "rule": { "spec": "d" } } },
              "rule": { "and": [ { "local": "is-active" }, { "local": "is active" }, { "local": "2fa" }, { "local": "class" } ] } }
            """, o => { o.Handles["a"] = "A"; o.Handles["b"] = "B"; o.Handles["c"] = "C"; o.Handles["d"] = "D"; });

        source.ShouldContain("""var isActive = Spec.Build(A).Create("is-active");""");
        source.ShouldContain("""var isActive2 = Spec.Build(B).Create("is active");""");
        source.ShouldContain("""var _2fa = Spec.Build(C).Create("2fa");""");
        source.ShouldContain("""var @class = Spec.Build(D).Create("class");""");
        source.ShouldContain("return (((isActive & isActive2) & _2fa) & @class);");
    }

    [Fact]
    public void Should_print_parameters_as_method_arguments_and_interpolation_as_an_interpolated_string()
    {
        var source = Body("""
            { "parameters": { "limit": { "type": "integer", "default": 3 }, "label": { "type": "string" }, "ratio": { "type": "number", "default": 1.5 }, "strict": { "type": "boolean", "default": false } },
              "rule": { "spec": "a", "whenTrue": "under {limit} for {label} {{literal}}", "whenFalse": "over" } }
            """, o => o.Handles["a"] = "A");

        source.ShouldContain("public static SpecBase<Customer, string> Build(SpecRegistry registry, string label, int limit = 3, double ratio = 1.5d, bool strict = false)");
        source.ShouldContain("""Spec.Build(A).WhenTrue($"under {limit} for {label} {{literal}}").WhenFalse("over").Create()""");
    }

    [Fact]
    public void Should_print_arguments_as_a_dictionary_literal()
    {
        var source = Body("""{ "rule": { "spec": "customer.older-than", "args": { "age": 18, "tag": "vip", "ratio": 2.5, "on": true, "none": null } } }""");

        source.ShouldContain("""registry.Get<Customer>("customer.older-than", new Dictionary<string, object?> { ["age"] = 18, ["tag"] = "vip", ["ratio"] = 2.5d, ["on"] = true, ["none"] = null })""");
    }

    [Fact]
    public void Should_print_a_higher_order_node_exactly_as_the_binder_builds_it()
    {
        var source = Body("""{ "parameters": { "min": { "type": "integer", "default": 2 } }, "rule": { "asAtLeastNSatisfied": { "spec": "is-positive" }, "n": "@min", "path": "orders", "name": "enough positive" } }""",
            o => { o.Handles["is-positive"] = "IsPositive"; o.Collections["orders"] = new CSharpCollectionHandle("int", "c => c.Orders"); });

        source.ShouldContain("""return Spec.Build(Spec.Build(IsPositive).AsAtLeastNSatisfied(min).WhenTrue($"at least {min} satisfied").WhenFalse($"fewer than {min} satisfied").Create().ChangeModelTo<Customer>(c => c.Orders)).Create("enough positive");""");
    }

    [Fact]
    public void Should_print_a_higher_order_node_without_a_collection_handle_as_a_todo()
    {
        var printed = CSharpPrinter.Print("""{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "account-orders" } }""",
            Options(o => o.Handles["is-positive"] = "IsPositive"));

        printed.Source.ShouldContain("""Spec.Build(Spec.Build(IsPositive).AsAllSatisfied().WhenTrue("all satisfied").WhenFalse("not all satisfied").Create().ChangeModelTo<Customer>(m => m.AccountOrders /* TODO: the collection registered at 'account-orders' */))""");
        printed.Warnings.ShouldContain(w => w.Contains("account-orders"));
    }

    [Fact]
    public void Should_print_object_payloads_as_todo_strings()
    {
        var printed = CSharpPrinter.Print("""{ "rule": { "spec": "a", "whenTrue": { "code": "OK" }, "whenFalse": { "code": "BAD" }, "name": "coded" } }""",
            Options(o => o.Handles["a"] = "A"));

        printed.Source.ShouldContain("""Spec.Build(A).WhenTrue("{\"code\":\"OK\"}").WhenFalse("{\"code\":\"BAD\"}").Create("coded") /* TODO: object payloads printed as strings */""");
        printed.Warnings.ShouldHaveSingleItem().ShouldContain("$.rule");
    }

    [Fact]
    public void Should_print_an_expression_leaf_verbatim_with_a_warning()
    {
        var printed = CSharpPrinter.Print("""{ "rule": { "expression": "m.Age > 18" } }""", Options());

        printed.Source.ShouldContain("Spec.From((Customer m) => m.Age > 18) /* expression printed verbatim; not re-parsed */");
        printed.Warnings.ShouldHaveSingleItem();
    }

    [Fact]
    public void Should_print_an_async_reference_through_get_async_and_return_the_async_type()
    {
        var source = Body("""{ "rule": { "and": [ { "spec": "customer.is-active-async" }, { "spec": "a" } ] } }""",
            o => { o.Async.Add("customer.is-active-async"); o.Handles["a"] = "A"; });

        source.ShouldContain("public static AsyncSpecBase<Customer, string> Build(SpecRegistry registry)");
        source.ShouldContain("""(registry.GetAsync<Customer>("customer.is-active-async") & A)""");
    }

    [Fact]
    public void Should_wrap_in_a_class_with_usings_and_a_namespace_when_asked()
    {
        var source = Body("""{ "rule": { "spec": "a", "args": { "n": 1 } } }""", o => { o.ClassName = "Eligible"; o.Namespace = "Shop.Rules"; });

        source.ShouldStartWith("using System.Collections.Generic;\nusing Motiv;\nusing Motiv.Serialization;\nusing Motiv.Serialization.Tests.Printing;\n\nnamespace Shop.Rules;\n\npublic static class Eligible\n{\n    public static SpecBase<Customer, string> Build(SpecRegistry registry)\n");
    }

    [Fact]
    public void Should_throw_the_parse_errors_for_a_document_that_does_not_parse()
    {
        Should.Throw<RuleSerializationException>(() => CSharpPrinter.Print("""{ "rule": { } }""", Options())).Errors.ShouldNotBeEmpty();
    }
}
```

The `Customer` record is nested inside the test class, so its C# name in the expectations is `Customer` and its namespace `Motiv.Serialization.Tests.Printing` — the printer uses `Type.Name` (with `+`-nesting rendered as `Outer.Inner` only for the model's own declaring chain; here it is fine to print `Customer` since the test's `using` puts it in scope). Decide once: **the model type prints as its simple `Name`, and class mode adds `using <Namespace>;`** — a nested model is the caller's problem and the design doc says so.

- [ ] **Step 2: Run red**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~CSharpPrinterTests" 2>&1 | grep -E "error CS" | sed -E 's/.*(error CS[0-9]+: [^[]*).*/\1/' | sort -u | head -3
```

Expected: `CSharpPrinter`, `CSharpPrintOptions` not found.

- [ ] **Step 3: Options and result records**

`src/Motiv.Serialization/Printing/CSharpPrintOptions.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>How a rule document is printed as C#. <see cref="ModelType"/> is never guessed: it is the rule's, or the registry entry's.</summary>
public sealed class CSharpPrintOptions
{
    private static readonly IReadOnlyDictionary<string, string> NoHandles = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, CSharpCollectionHandle> NoCollections = new Dictionary<string, CSharpCollectionHandle>();
    private static readonly ISet<string> NoAsync = new HashSet<string>();

    /// <summary>The model type the printed method is over.</summary>
    public required Type ModelType { get; init; }

    /// <summary>The C# expression an unmapped spec reference resolves through; the printed method's first parameter.</summary>
    public string RegistryExpression { get; init; } = "registry";

    /// <summary>Spec name → the C# expression that is that spec (a compiled handle such as <c>Specs.IsActive</c>). Unmapped names go through <see cref="RegistryExpression"/>.</summary>
    public IReadOnlyDictionary<string, string> SpecHandles { get; init; } = NoHandles;

    /// <summary>Collection path → its element type and selector, for higher-order nodes. Unmapped paths print a <c>TODO</c>.</summary>
    public IReadOnlyDictionary<string, CSharpCollectionHandle> Collections { get; init; } = NoCollections;

    /// <summary>The spec names registered as async; a reference to one prints through <c>GetAsync</c> and makes the method async-typed.</summary>
    public ISet<string> AsyncSpecs { get; init; } = NoAsync;

    /// <summary>The namespace to emit, or null for none. Only used with <see cref="ClassName"/>.</summary>
    public string? Namespace { get; init; }

    /// <summary>The static class to wrap the method in, or null to print the method alone.</summary>
    public string? ClassName { get; init; }

    /// <summary>The printed method's name.</summary>
    public string MethodName { get; init; } = "Build";

    /// <summary>The options the document is parsed with, or null for defaults.</summary>
    public RuleSerializerOptions? SerializerOptions { get; init; }
}

/// <summary>What a higher-order node needs to print over a registered collection.</summary>
/// <param name="ElementType">The C# name of the element type (<c>int</c>, <c>Order</c>).</param>
/// <param name="Selector">The lambda from the model to the collection (<c>c =&gt; c.Orders</c>), or null to print a <c>TODO</c>.</param>
public sealed record CSharpCollectionHandle(string ElementType, string? Selector);

/// <summary>The printed source and every place it needs a person's attention.</summary>
public sealed record CSharpPrintedRule(string Source, IReadOnlyList<string> Warnings);
```

- [ ] **Step 4: The printer**

`src/Motiv.Serialization/Printing/CSharpPrinter.cs` — one static class with a private `Emitter` instance per print. Skeleton to write out in full:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>
/// Prints a rule document as the C# builder chain that compiles to the same specification:
/// definitions as locals, parameters as arguments, references through handles or the registry,
/// higher-order nodes as exactly the chain the binder builds. Every place the print cannot be
/// exact — an expression leaf, an object payload, a collection with no handle — is a warning.
/// </summary>
public static class CSharpPrinter
{
    public static CSharpPrintedRule Print(string documentJson, CSharpPrintOptions options)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));
        if (options is null) throw new ArgumentNullException(nameof(options));
        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(options.SerializerOptions ?? new RuleSerializerOptions()).Parse(documentJson, errors);
        if (document is null || errors.Count > 0)
            throw new RuleSerializationException(errors);
        return Print(document, options);
    }

    internal static CSharpPrintedRule Print(RuleDocument document, CSharpPrintOptions options) =>
        new Emitter(document, options).Emit();

    private sealed class Emitter(RuleDocument document, CSharpPrintOptions options)
    {
        private readonly List<string> _warnings = [];
        private readonly Dictionary<string, string> _locals = new(StringComparer.Ordinal);   // definition key -> identifier
        private readonly HashSet<string> _taken = new(StringComparer.Ordinal);
        private bool _usesDictionary;
        private bool _isAsync;
        private string Model => options.ModelType.Name;

        public CSharpPrintedRule Emit()
        {
            // Names first, so a local referenced before its declaration order still resolves.
            foreach (var definition in document.Definitions)
                _locals[definition.Name!] = Identifier(definition.Name!);

            var body = new StringBuilder();
            foreach (var definition in document.Definitions)
                body.Append("    var ").Append(_locals[definition.Name!]).Append(" = ").Append(Node(definition)).Append(";\n");
            var root = Node(document.Root!);
            var returned = document.Name is null ? root : $"Spec.Build({root}).Create({Literal(document.Name)})";
            body.Append("    return ").Append(returned).Append(";\n");

            var method = new StringBuilder()
                .Append("public static ").Append(_isAsync ? "AsyncSpecBase" : "SpecBase").Append('<').Append(Model).Append(", string> ")
                .Append(options.MethodName).Append("(SpecRegistry ").Append(options.RegistryExpression).Append(Parameters()).Append(")\n{\n")
                .Append(body).Append("}\n");

            return new CSharpPrintedRule(options.ClassName is null ? method.ToString() : Wrap(method.ToString()), _warnings);
        }
        // Parameters(): required first then defaulted, in declaration order within each group.
        // Wrap(): usings (System.Collections.Generic when _usesDictionary; Motiv; Motiv.Serialization; the model's namespace), namespace, class, method indented by four.
        // Node(node): the decorated expression — Operator switch for the core, then Decorate.
        // Core: Spec -> handle or Get/GetAsync (+ args dictionary); Local -> _locals[node.LocalName]; Expression -> Spec.From(...) + warning;
        //       Not -> "!" + Core(child... via Node); And/Or/XOr -> fold "(" a op b ")"; AndAlso/OrElse -> fold a.AndAlso(b); higher-order -> HigherOrder(node).
        // HigherOrder(node): handle = options.Collections.TryGetValue(node.PathText) ...; inner = new Emitter-scoped Node over the child with Model temporarily = element type (keep a stack of model names);
        //       texts per HigherOrder.Build; n literal or parameter identifier with $"" texts; ChangeModelTo<Model>(selector or TODO).
        // Decorate(node, core): whenTrue text/object handling, name.
        // Text(string): "$\"...\"" when it contains '{' or '}', else Literal(...).
        // Literal(string): C# string literal with escapes. Scalar(object?): int/long/double/string/bool/null.
        // Identifier(key): camelCase, sanitise, digit prefix, keyword @, dedupe via _taken.
    }
}
```

Write every helper in full — no placeholders in the code. Keywords list: the C# reserved words (`abstract` … `while`; copy from the language spec, ~77 entries). PascalCase for the collection `TODO` member: split on `-`, `_`, space; capitalise each part.

- [ ] **Step 5: Run green; then the whole project on net10 and a netstandard2.0 build of the library; commit**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//'
env -u MallocStackLogging -u MallocNanoZone dotnet build src/Motiv.Serialization -f netstandard2.0 2>&1 | grep -E "error CS|Build succeeded"
git add src/Motiv.Serialization/Printing src/Motiv.Serialization.Tests/Printing
git commit -m "C# printer — a rule document as the builder chain that compiles to the same specification

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: The fidelity corpus

**Files:**
- Create: `src/Motiv.Serialization.Tests/Printing/Corpus/*.json` (sixteen documents, listed below)
- Create: `src/Motiv.Serialization.Tests/Printing/PrintedRuleCompiler.cs` (Roslyn harness, `#if !NETFRAMEWORK`)
- Create: `src/Motiv.Serialization.Tests/Printing/PrinterFidelityTests.cs`
- Modify: `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj` (`<PackageReference Include="Microsoft.CodeAnalysis.CSharp" />`, `<EmbeddedResource Include="Printing\Corpus\*.json" />`)

**Interfaces:**
- Consumes: `CSharpPrinter.Print`, `SpecRegistry.Get`.
- Produces: `PrintedRuleCompiler.Compile(string source, string className) → Type` and `Invoke(Type, SpecRegistry) → SpecBase<PrinterCustomer, string>` (defaults for every parameter).

- [ ] **Step 1: The corpus**

A public model and registry in `PrinterFidelityTests`:

```csharp
public sealed record PrinterCustomer(bool IsActive, int Age, int OrderCount, IReadOnlyList<int> Orders);

public static class PrinterSpecs
{
    public static SpecBase<PrinterCustomer, string> IsActive { get; } = Spec.Build((PrinterCustomer c) => c.IsActive).WhenTrue("customer is active").WhenFalse("customer is inactive").Create();
    public static SpecBase<PrinterCustomer, string> IsAdult { get; } = Spec.Build((PrinterCustomer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();
    public static SpecBase<PrinterCustomer, string> HasOrders { get; } = Spec.Build((PrinterCustomer c) => c.OrderCount > 0).WhenTrue("has orders").WhenFalse("no orders").Create();
    public static SpecBase<int, string> IsPositive { get; } = Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();
    public static SpecRegistry Registry() => new SpecRegistry()
        .Register("customer.is-active", IsActive).Register("customer.is-adult", IsAdult).Register("customer.has-orders", HasOrders).Register("is-positive", IsPositive)
        .RegisterParameterised<PrinterCustomer>("customer.min-orders", [new RuleParameterDeclaration("n", RuleParameterType.Integer, false, null)],
            args => Spec.Build((PrinterCustomer c) => c.OrderCount >= (int)args["n"]!).WhenTrue($"at least {args["n"]} orders").WhenFalse($"fewer than {args["n"]} orders").Create())
        .RegisterCollection<PrinterCustomer, int>("orders", c => c.Orders);
}
```

Documents (each its own file; the name is the test's display name):

| File | Document |
|---|---|
| `01-spec-leaf.json` | `{ "rule": { "spec": "customer.is-active" } }` |
| `02-named-root.json` | `{ "name": "eligible", "rule": { "spec": "customer.is-active" } }` |
| `03-and.json` | `{ "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] } }` |
| `04-or-xor.json` | `{ "rule": { "or": [ { "spec": "customer.is-active" }, { "xor": [ { "spec": "customer.is-adult" }, { "spec": "customer.has-orders" } ] } ] } }` |
| `05-not.json` | `{ "rule": { "not": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.has-orders" } ] } } }` |
| `06-and-also-or-else.json` | `{ "rule": { "orElse": [ { "andAlso": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] }, { "spec": "customer.has-orders" } ] } }` |
| `07-decorated-node.json` | `{ "rule": { "and": [ { "spec": "customer.is-active", "whenTrue": "active", "whenFalse": "inactive" }, { "spec": "customer.is-adult" } ] } }` |
| `08-decorated-named-node.json` | `{ "rule": { "or": [ { "spec": "customer.is-active", "whenTrue": "active", "whenFalse": "inactive", "name": "activity" }, { "spec": "customer.has-orders", "name": "ordering" } ], "name": "either" } }` |
| `09-definitions-and-local.json` | `{ "definitions": { "is-active": { "rule": { "spec": "customer.is-active" } }, "grown up": { "rule": { "spec": "customer.is-adult", "whenTrue": "grown", "whenFalse": "young" } } }, "rule": { "and": [ { "local": "is-active" }, { "local": "grown up" }, { "not": { "local": "is-active" } } ] } }` |
| `10-parameters-with-n.json` | `{ "parameters": { "min": { "type": "integer", "default": 2 }, "who": { "type": "string", "default": "the customer" } }, "rule": { "asAtLeastNSatisfied": { "spec": "is-positive" }, "n": "@min", "path": "orders", "whenTrue": "{who} has {min}+ positive {{orders}}", "whenFalse": "{who} does not" } }` |
| `11-args.json` | `{ "rule": { "and": [ { "spec": "customer.min-orders", "args": { "n": 2 } }, { "spec": "customer.is-active" } ] } }` |
| `12-higher-order-all-any.json` | `{ "rule": { "and": [ { "asAllSatisfied": { "spec": "is-positive" }, "path": "orders" }, { "asAnySatisfied": { "spec": "is-positive" }, "path": "orders", "name": "some positive" } ] } }` |
| `13-higher-order-n.json` | `{ "rule": { "or": [ { "asNSatisfied": { "spec": "is-positive" }, "n": 2, "path": "orders" }, { "asAtMostNSatisfied": { "not": { "spec": "is-positive" } }, "n": 1, "path": "orders" } ] } }` |
| `14-n-ary-and.json` | `{ "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" }, { "spec": "customer.has-orders" }, { "not": { "spec": "customer.is-adult" } } ] } }` |
| `15-nested-mixed.json` | `{ "name": "checkout", "definitions": { "trusted": { "rule": { "andAlso": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ], "name": "trusted" } } }, "rule": { "orElse": [ { "local": "trusted" }, { "and": [ { "spec": "customer.has-orders" }, { "asAllSatisfied": { "spec": "is-positive" }, "path": "orders" } ], "whenTrue": "good history", "whenFalse": "no history" } ] } }` |
| `16-loyalty-discount.json` | Studio's `src/Motiv.Studio/Rules/loyalty-discount.json` verbatim (`audited` is ignored by the printer). |

Scenarios: `new PrinterCustomer(true, 30, 3, [1, 2, 3])`, `(true, 16, 1, [5])`, `(false, 41, 12, [-1, 4])`, `(true, 25, 0, [])`, `(false, 17, 2, [0, 0])`, `(true, 70, 2, [-3, -4])`.

- [ ] **Step 2: The Roslyn harness**

`PrintedRuleCompiler.cs`, wrapped in `#if !NETFRAMEWORK`:

```csharp
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Motiv.Serialization.Tests.Printing;

internal static class PrintedRuleCompiler
{
    private static readonly MetadataReference[] References = BuildReferences();

    public static Type Compile(string source, string @namespace, string className)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            $"printed-{Guid.NewGuid():N}", [tree], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException("printed C# did not compile:\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) + "\n\n" + source);
        var assembly = Assembly.Load(stream.ToArray());
        return assembly.GetType($"{@namespace}.{className}") ?? throw new InvalidOperationException($"{className} not found in printed assembly");
    }

    /// <summary>Invokes the printed Build with the registry and every parameter at its default.</summary>
    public static object Invoke(Type printed, string methodName, SpecRegistry registry)
    {
        var method = printed.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
        var arguments = method.GetParameters().Select(p => p.Position == 0 ? registry : p.HasDefaultValue ? p.DefaultValue : throw new InvalidOperationException($"'{p.Name}' has no default")).ToArray();
        return method.Invoke(null, arguments)!;
    }

    private static MetadataReference[] BuildReferences()
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trusted
            .Concat([typeof(Spec).Assembly.Location, typeof(SpecRegistry).Assembly.Location, typeof(PrintedRuleCompiler).Assembly.Location])
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
#endif
```

- [ ] **Step 3: The theory**

```csharp
#if !NETFRAMEWORK
public class PrinterFidelityTests
{
    public static TheoryData<string> Documents => [.. typeof(PrinterFidelityTests).Assembly.GetManifestResourceNames().Where(n => n.Contains(".Printing.Corpus.")).Select(n => n[(n.IndexOf(".Corpus.", StringComparison.Ordinal) + 8)..]).Order()];

    [Theory]
    [MemberData(nameof(Documents))]
    public void Should_print_c_sharp_that_decides_and_explains_as_the_document_does(string file)
    {
        // Arrange — the document, bound; and its print, compiled
        var json = Read(file);
        var registry = PrinterSpecs.Registry();
        var bound = new RuleSerializer(registry).Deserialize<PrinterCustomer>(json);
        var printed = CSharpPrinter.Print(json, new CSharpPrintOptions
        {
            ModelType = typeof(PrinterCustomer), ClassName = "Printed", Namespace = "Motiv.Serialization.Tests.Printed",
            Collections = new Dictionary<string, CSharpCollectionHandle> { ["orders"] = new("int", "c => c.Orders") },
        });
        printed.Warnings.ShouldBeEmpty(file);
        var compiled = (SpecBase<PrinterCustomer, string>)PrintedRuleCompiler.Invoke(PrintedRuleCompiler.Compile(printed.Source, "Motiv.Serialization.Tests.Printed", "Printed"), "Build", registry);

        // Act & Assert — every scenario decides and explains the same
        foreach (var customer in Scenarios)
        {
            var expected = bound.Evaluate(customer);
            var actual = compiled.Evaluate(customer);
            actual.Satisfied.ShouldBe(expected.Satisfied, $"{file}: {customer}");
            actual.Assertions.ShouldBe(expected.Assertions, ignoreOrder: true, $"{file}: {customer}");
            actual.Justification.ShouldBe(expected.Justification, $"{file}: {customer}");
        }
    }
}
#endif
```

`Justification` equality is stricter than the spec asks (assertions as sets) and is what proves the fold order; keep it, and if a corpus document legitimately differs only in justification shape, ledger a ruling and drop to assertions for that one — do not drop it globally. Note the printed method's parameters default; `10-parameters-with-n.json` binds with `Deserialize<PrinterCustomer>(json)` (defaults) on both sides.

- [ ] **Step 4: Run; fix the printer until every document passes; commit**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PrinterFidelityTests" 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed |did not compile" | sed -E 's/ \[.*//' | head -20
git add src/Motiv.Serialization.Tests
git commit -m "C# printer — fidelity corpus: sixteen documents print, compile and decide as they bind

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: `Reproduction.CSharp`

**Files:**
- Modify: `src/Motiv.Serialization/Decisions/Reproduction.cs` (add `string? CSharp` and `IReadOnlyList<string> CSharpWarnings` at the end of the positional record)
- Modify: `src/Motiv.Serialization/Decisions/DecisionReproducer.cs`
- Modify: `src/Motiv.Serialization.Tests/Decisions/DecisionReproducerTests.cs`

**Interfaces:**
- Produces: `Reproduction.CSharp` — the pinned rule document printed with `ModelType = rule.ModelType`, no handles, `AsyncSpecs` from `rules.Scope.Registry.Entries.Where(e => e.IsAsync)`, `Collections` from `rules.Scope.Registry.Collections` where `ParentType == rule.ModelType` (element type `Type.Name`, selector null), `ClassName = PascalCase(rule name) + "Rule"`, `MethodName = "Build"`. Null when the pinned version has no document (a revert) or the rule version is missing.

- [ ] **Step 1: Tests** (in `DecisionReproducerTests`)

```csharp
    [Fact]
    public async Task Should_print_the_pinned_document_as_c_sharp()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.CSharp.ShouldNotBeNull();
        reproduction.CSharp.ShouldContain("public static class CanCheckoutRule");
        reproduction.CSharp.ShouldContain("""return registry.Get<Customer>("customer.eligible");""");
        reproduction.CSharpWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_print_nothing_for_a_reverted_version()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        (await host.Rules.RevertAsync("can-checkout", 2, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var forged = decision with { Id = Guid.NewGuid(), RuleVersion = 3, ReferencedPropositionVersions = [] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.CSharp.ShouldBeNull();
    }
```

- [ ] **Step 2: Run red, implement, run the project green, commit**

In `ReproduceAsync`, after the replay: `var (csharp, warnings) = Print(rule, ruleVersion);` where `Print` returns `(null, [])` when `rule is null || ruleVersion?.DocumentJson is null`, else calls `CSharpPrinter.Print(ruleVersion.DocumentJson, options)` inside a `try` — a `RuleSerializationException` (the document parsed for binding, so this is defensive) becomes a warning. `PascalCase`: split the rule name on `-`, `_`, `.`, space; capitalise each part; the same helper the printer uses for the collection `TODO` — make it `internal static string CSharpIdentifiers.PascalCase(string)` in the Printing folder and use it from both.

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//'
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "Decision reproducer — Reproduction.CSharp: the pinned document printed, or null for a revert

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Docs, design doc, full verification, simplifier, PR

- [ ] **Docs:** new `docs/live-rules/csharp-printer.md` (what it prints, the options table, the node table from spec §6 as implemented, warnings and the three `TODO` shapes, `SpecRegistry.Get`, the fidelity contract); add to `docs/live-rules/toc.yml` and the table in `docs/live-rules/index.md`; `docs/decision-log/replay.md` gains a paragraph on `Reproduction.CSharp` and `CSharpWarnings`; `CONTEXT.md` glossary gains **Printed rule**; parent spec §6 amended (`registry.Get<TModel>("name")` is now real; collections need a handle or print a `TODO`; the fidelity test compares justification too).
- [ ] **Design doc:** `docs/superpowers/specs/2026-09-20-csharp-printer-design.md` — decisions: printer over the parsed document, not the UI; `Get` on the registry; handles and collections as options; higher-order texts mirror the binder; interpolation prints as C# interpolation; object payloads and expressions as warned stand-ins; `CSharp` on the reproduction with warnings beside it; corpus as embedded resources; Roslyn harness guarded off net472.
- [ ] **Full verification:** solution build, every test project, net8/net9 for `Motiv.Serialization.Tests`; e2e (Studio host unchanged, but the printer is in its dependency).
- [ ] **Simplifier** over `git diff claude/decision-reproducer...HEAD -- src/`; apply; re-run.
- [ ] **PR** against `claude/decision-reproducer`.
