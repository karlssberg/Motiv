#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafCompilerTests
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum Kind { Retail, Wholesale }

    private enum Tier { Bronze, Silver }

    private sealed record Item(int Qty);
    private sealed record Order(string Status, decimal Total, int DaysSinceShipped, IReadOnlyList<Item>? Items = null);
    private sealed record Customer(
        int Age, bool IsActive, decimal CreditLimit, string? Country, IReadOnlyList<Order>? Orders,
        Kind Kind = Kind.Retail, Tier Tier = Tier.Silver);

    private static readonly RuleParameterDeclaration[] Declarations =
    [
        new("minAge", RuleParameterType.Integer, true, 18),
        new("vip", RuleParameterType.Number, true, 1000d),
    ];

    private static readonly Dictionary<string, object?> Values = new() { ["minAge"] = 18, ["vip"] = 1000d };

    private static SpecBase<Customer, string> Compile(string text)
    {
        var problems = new List<LeafProblem>();
        var node = LeafParser.Parse(text, problems)!;
        var analysis = LeafChecker.Check(node, LeafScope.For(typeof(Customer), Declarations));
        analysis.IsValid.ShouldBeTrue(string.Join("; ", analysis.Problems.Select(p => p.Message)));
        return LeafCompiler.Compile<Customer>(node, analysis, Values, text);
    }

    private static readonly Customer Sample = new(34, true, 800m, "SE",
        [new("paid", 620m, 12, [new(2)]), new("paid", 410m, 40), new("pending", 950m, 0), new("refunded", 75m, 90)]);

    [Fact]
    public void Should_compile_a_filtered_aggregate_and_explain_it_like_a_compiled_expression()
    {
        var spec = Compile("orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip");
        var result = spec.Evaluate(Sample);

        result.Satisfied.ShouldBeTrue();
        result.Reason.ShouldBe("orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip == true");
        result.Assertions.ShouldHaveSingleItem().ShouldContain("Sum");
    }

    [Fact]
    public void Should_type_the_parameter_from_the_anchor_so_decimal_meets_decimal()
    {
        var spec = Compile("creditLimit > @vip");
        spec.Evaluate(Sample).Satisfied.ShouldBeFalse();
        spec.Evaluate(Sample with { CreditLimit = 1000.5m }).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_keep_a_fractional_literal_exact_against_a_decimal_field()
    {
        var spec = Compile("creditLimit * 0.1 == 80");
        spec.Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_widen_an_int_field_to_decimal_when_a_fractional_literal_meets_it()
    {
        Compile("age * 1.5 > 50").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_never_throw_on_a_null_path()
    {
        var noOrders = Sample with { Orders = null, Country = null };
        Compile("orders.sum(o => o.total) > 1").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("orders.count() >= 0").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("orders == null").Evaluate(noOrders).Satisfied.ShouldBeTrue();
        Compile("country != null").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("country.equalsIgnoreCase(\"se\")").Evaluate(noOrders).Satisfied.ShouldBeFalse();
        Compile("orders.all(o => o.total <= creditLimit)").Evaluate(noOrders).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_fold_a_null_comparison_against_a_non_nullable_value_type()
    {
        // The checker only warns here ("age is never null"), so the compiler must still produce a
        // tree: `Expression.Constant(null, typeof(int))` would throw and escape Validate/Deserialize.
        Compile("age == null").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("age != null").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_compile_a_string_converted_enum_by_name_and_any_other_by_number()
    {
        Compile("kind == \"Retail\"").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("kind == \"Wholesale\"").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("kind != \"Retail\"").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("tier == 1").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("tier > 1").Evaluate(Sample).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_compare_strings_ordinally_and_ignore_case_only_when_asked()
    {
        Compile("country == \"se\"").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("country.equalsIgnoreCase(\"se\")").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_reach_the_parent_from_inside_a_lambda()
    {
        Compile("orders.all(o => o.total <= creditLimit)").Evaluate(Sample).Satisfied.ShouldBeFalse();
        Compile("orders.any(o => o.total > creditLimit)").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_compile_subtraction_and_boolean_literals()
    {
        Compile("age - 4 == 30").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("isActive == true").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("isActive == false").Evaluate(Sample).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_use_checked_arithmetic()
    {
        var spec = Compile("age * 2000000000 > 1");
        Should.Throw<OverflowException>(() => spec.Evaluate(Sample));
    }

    [Fact]
    public void Should_short_circuit_boolean_connectives_and_negate()
    {
        Compile("!isActive || age >= @minAge").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("isActive && age < @minAge").Evaluate(Sample).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_compile_min_and_max_aggregates()
    {
        Compile("orders.min(o => o.total) >= 75").Evaluate(Sample).Satisfied.ShouldBeTrue();
        Compile("orders.max(o => o.total) <= creditLimit").Evaluate(Sample).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_restore_the_outer_lambda_binding_after_a_nested_lambda_shadows_it()
    {
        Compile("orders.any(o => o.items.any(o => o.qty > 0) || o.total > 100)").Evaluate(Sample).Satisfied.ShouldBeTrue();
    }
}
#endif
