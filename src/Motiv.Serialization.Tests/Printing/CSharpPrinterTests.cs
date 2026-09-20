using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Printing;

/// <summary>
/// The printer emits the builder chain a document binds to, line for line: references through
/// handles or the registry, compositions folded as the binder folds them, decorations as the
/// builder, higher-order nodes exactly as <c>HigherOrder.Build</c> makes them.
/// </summary>
public class CSharpPrinterTests
{
    public sealed record Customer(bool IsActive, int Age, IReadOnlyList<int> Orders);

    private sealed class OptionsBuilder
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

    private static CSharpPrintOptions Options(Action<OptionsBuilder>? configure = null)
    {
        var builder = new OptionsBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    private static string Body(string json, Action<OptionsBuilder>? configure = null) =>
        CSharpPrinter.Print(json, Options(configure)).Source;

    [Fact]
    public void Should_print_a_spec_leaf_through_the_registry_when_no_handle_is_supplied()
    {
        var source = Body("""{ "rule": { "spec": "customer.is-active" } }""");

        source.ShouldBe(
            "public static SpecBase<Customer, string> Build(SpecRegistry registry)\n" +
            "{\n" +
            "    return registry.Get<Customer>(\"customer.is-active\");\n" +
            "}\n");
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
                "is_active": { "rule": { "spec": "b" } },
                "_2fa": { "rule": { "spec": "c" } },
                "class": { "rule": { "spec": "d" } } },
              "rule": { "and": [ { "local": "is-active" }, { "local": "is_active" }, { "local": "_2fa" }, { "local": "class" } ] } }
            """, o => { o.Handles["a"] = "A"; o.Handles["b"] = "B"; o.Handles["c"] = "C"; o.Handles["d"] = "D"; });

        source.ShouldContain("""var isActive = Spec.Build(A).Create("is-active");""");
        source.ShouldContain("""var isActive2 = Spec.Build(B).Create("is_active");""");
        source.ShouldContain("""var _2fa = Spec.Build(C).Create("_2fa");""");
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

        printed.Source.ShouldContain("""Spec.Build(IsPositive).AsAllSatisfied().WhenTrue("all satisfied").WhenFalse("not all satisfied").Create().ChangeModelTo<Customer>(m => m.AccountOrders /* TODO: the collection registered at 'account-orders' */)""");
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

        source.ShouldStartWith(
            "using System.Collections.Generic;\nusing Motiv;\nusing Motiv.Serialization;\nusing Motiv.Serialization.Tests.Printing;\n\n" +
            "namespace Shop.Rules;\n\npublic static class Eligible\n{\n    public static SpecBase<Customer, string> Build(SpecRegistry registry)\n");
    }

    [Fact]
    public void Should_throw_the_parse_errors_for_a_document_that_does_not_parse()
    {
        Should.Throw<RuleSerializationException>(() => CSharpPrinter.Print("""{ "rule": { } }""", Options())).Errors.ShouldNotBeEmpty();
    }
}
