#if NET8_0_OR_GREATER
namespace Motiv.Serialization.Tests.Expressions;

public class LeafBindingTests
{
    public sealed record Order(string Status, decimal Total);
    public sealed record Customer(int Age, decimal CreditLimit, IReadOnlyList<Order>? Orders);
    public sealed record Code(int code);

    private static readonly Customer Sample = new(34, 800m, [new("paid", 620m), new("paid", 410m), new("pending", 950m)]);

    private static RuleSerializer Serializer() => new(new SpecRegistry()
        .Register("is-adult", Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create()));

    [Fact]
    public void Should_bind_a_leaf_with_a_document_parameter()
    {
        const string json = """
            { "parameters": { "vip": { "type": "number", "default": 1000 } },
              "rule": { "andAlso": [ { "spec": "is-adult" },
                        { "expression": "orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip",
                          "whenTrue": "big spender", "whenFalse": "not a big spender" } ] } }
            """;
        var spec = Serializer().Deserialize<Customer>(json);

        var result = spec.Evaluate(Sample);
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["adult", "big spender"]);

        var overridden = Serializer().Deserialize<Customer>(json, new { vip = 2000 });
        overridden.Evaluate(Sample).Assertions.ShouldBe(["not a big spender"]);
    }

    [Fact]
    public void Should_explain_an_undecorated_leaf_with_its_decomposed_clause()
    {
        const string json = """{ "rule": { "expression": "creditLimit > 500" } }""";
        var result = Serializer().Deserialize<Customer>(json).Evaluate(Sample);
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["model.CreditLimit > 500"]);
        result.Reason.ShouldBe("creditLimit > 500 == true");
    }

    [Fact]
    public void Should_report_a_type_problem_with_its_range_inside_the_leaf()
    {
        const string json = """{ "rule": { "expression": "orders.sum(o => o.nope) > 1" } }""";
        var errors = Serializer().Validate<Customer>(json);
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownField);
        error.Path.ShouldBe("$.rule");
        error.Range.ShouldBe(new RuleTextRange(18, 22));
    }

    [Fact]
    public void Should_bind_inside_a_quantifier_body_against_the_element()
    {
        const string json = """
            { "rule": { "asAllSatisfied": { "expression": "total <= 1000" }, "path": "orders" } }
            """;
        var serializer = new RuleSerializer(new SpecRegistry().RegisterCollection<Customer, Order>("orders", c => c.Orders ?? []));
        serializer.Deserialize<Customer>(json).Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_gather_facts_for_a_leaf_inside_a_quantifier_body_against_the_element()
    {
        const string json = """
            { "rule": { "asAllSatisfied": { "expression": "total > 10" }, "path": "orders" } }
            """;
        var serializer = new RuleSerializer(new SpecRegistry().RegisterCollection<Customer, Order>("orders", c => c.Orders ?? []));
        var inspection = serializer.Inspect<Customer>(json);
        inspection.Errors.ShouldBeEmpty();
        var literal = inspection.Facts.Single(f => f.Text == "10");
        literal.Type.ShouldBe("decimal");
    }

    [Fact]
    public void Should_gather_a_referenced_definitions_facts_only_once()
    {
        const string json = """
            { "definitions": { "is-big": { "rule": { "expression": "creditLimit > 500" } } },
              "rule": { "and": [ { "local": "is-big" }, { "local": "is-big" } ] } }
            """;
        var inspection = Serializer().Inspect<Customer>(json);
        inspection.Errors.ShouldBeEmpty();
        inspection.Facts.Count(f => f.Text == "500").ShouldBe(1);
        inspection.Facts.Count(f => f.Text == "creditLimit > 500").ShouldBe(1);
    }

    [Fact]
    public async Task Should_bind_in_an_async_load()
    {
        const string json = """{ "rule": { "expression": "age >= 18" } }""";
        var spec = Serializer().DeserializeAsyncSpec<Customer>(json);
        (await spec.EvaluateAsync(Sample)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_require_metadata_on_a_leaf_in_a_metadata_load()
    {
        const string bare = """{ "rule": { "expression": "age >= 18" } }""";
        var errors = Serializer().Validate<Customer, int>(bare);
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ExpressionRequiresMetadata);

        const string decorated = """{ "rule": { "expression": "age >= 18", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 }, "name": "adult" } }""";
        Serializer().Deserialize<Customer, Code>(decorated).Evaluate(Sample).Values.ShouldHaveSingleItem().code.ShouldBe(1);
    }
}
#endif
