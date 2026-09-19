#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafCheckerTests
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum Kind { Retail, Wholesale }

    private enum Tier { Bronze, Silver }

    private sealed record Order(string Status, decimal Total, int DaysSinceShipped);
    private sealed record Customer(int Age, long Points, double Score, bool IsActive, decimal CreditLimit, string? Country, IReadOnlyList<Order>? Orders, DateTime? ShippedAt, [property: JsonIgnore] int Hidden, Kind Kind, Tier Tier, Kind? MaybeKind);

    private static readonly RuleParameterDeclaration[] Parameters =
    [
        new("minAge", RuleParameterType.Integer, true, 18),
        new("vip", RuleParameterType.Number, true, 1000d),
        new("flag", RuleParameterType.Boolean, true, true),
    ];

    private static LeafAnalysis Check(string text)
    {
        var problems = new List<LeafProblem>();
        var node = LeafParser.Parse(text, problems) ?? throw new Exception(string.Join("; ", problems.Select(p => p.Message)));
        return LeafChecker.Check(node, LeafScope.For(typeof(Customer), Parameters));
    }

    [Fact]
    public void Should_type_a_literal_from_the_field_across_the_comparison()
    {
        var analysis = Check("creditLimit > 1000");
        analysis.IsValid.ShouldBeTrue();
        var fact = analysis.Facts.Single(f => f.Node is NumberLiteral);
        fact.Type.ShouldBe(typeof(decimal));
        fact.From.ShouldNotBeNull();
        fact.From.ShouldBe("creditLimit");
    }

    [Fact]
    public void Should_type_a_whole_untyped_subtree_from_the_other_side()
    {
        var analysis = Check("@vip * 2 > orders.sum(o => o.total)");
        analysis.IsValid.ShouldBeTrue();
        analysis.Facts.Single(f => f.Node is ParameterRef).Type.ShouldBe(typeof(decimal));
        analysis.Facts.Single(f => f.Node is NumberLiteral).Type.ShouldBe(typeof(decimal));
        var numberFact = analysis.Facts.Single(f => f.Node is NumberLiteral);
        numberFact.From.ShouldNotBeNull();
        numberFact.From.ShouldBe("orders.sum(o => o.total)");
    }

    [Fact]
    public void Should_widen_an_integer_field_when_a_fractional_literal_meets_it()
    {
        var analysis = Check("age * 1.5 > 40");
        analysis.IsValid.ShouldBeTrue();
        analysis.Facts.Single(f => f.Node is NumberLiteral { Text: "1.5" }).Type.ShouldBe(typeof(decimal));
        analysis.Facts.Single(f => f.Node is NumberLiteral { Text: "40" }).Type.ShouldBe(typeof(decimal));
    }

    [Fact]
    public void Should_refuse_decimal_against_double()
    {
        var analysis = Check("creditLimit > score");
        var problem = analysis.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
        problem.Message.ShouldContain("decimal");
        problem.Message.ShouldContain("double");
    }

    [Fact]
    public void Should_refuse_a_number_parameter_against_an_integer_anchor()
    {
        var analysis = Check("orders.count() >= @vip");
        analysis.Problems.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
    }

    [Fact]
    public void Should_default_an_unanchored_subtree_with_a_warning()
    {
        var analysis = Check("1 + 1 > 1");
        analysis.IsValid.ShouldBeTrue();
        analysis.Problems.ShouldAllBe(p => p.IsWarning);
        analysis.Facts.Where(f => f.Node is NumberLiteral).ShouldAllBe(f => f.Type == typeof(int) && f.From == null);
    }

    [Fact]
    public void Should_warn_on_integer_division()
    {
        var analysis = Check("age / 2 > 10");
        analysis.Problems.ShouldHaveSingleItem().Message.ShouldContain("truncates");
        analysis.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("nope > 1", RuleErrorCode.UnknownField, "nope")]
    [InlineData("orders.total > 1", RuleErrorCode.UnknownMethod, "collection")]
    [InlineData("orders.sum(o => o.nope) > 1", RuleErrorCode.UnknownField, "nope")]
    [InlineData("orders.first() != null", RuleErrorCode.UnknownMethod, "first")]
    [InlineData("age.count() > 1", RuleErrorCode.UnknownMethod, "collection")]
    [InlineData("orders.where(o => o.total) > 1", RuleErrorCode.ExpressionTypeMismatch, "condition")]
    [InlineData("orders.sum(o => o.status) > 1", RuleErrorCode.ExpressionTypeMismatch, "number")]
    [InlineData("country == 3", RuleErrorCode.ExpressionTypeMismatch, "string")]
    [InlineData("age && isActive", RuleErrorCode.ExpressionTypeMismatch, "condition")]
    [InlineData("age + 1", RuleErrorCode.ExpressionTypeMismatch, "leaf must be a condition")]
    [InlineData("@nope > 1", RuleErrorCode.UnknownField, "parameter")]
    [InlineData("hidden > 1", RuleErrorCode.UnknownField, "hidden")]
    [InlineData("age / 0 > 1", RuleErrorCode.ExpressionTypeMismatch, "division by zero")]
    [InlineData("kind == \"Nope\"", RuleErrorCode.ExpressionTypeMismatch, "\"Retail\"")]
    [InlineData("tier == \"Bronze\"", RuleErrorCode.ExpressionTypeMismatch, "number")]
    [InlineData("creditLimit / 0.0 > 1", RuleErrorCode.ExpressionTypeMismatch, "division by zero")]
    [InlineData("orders > 1", RuleErrorCode.ExpressionTypeMismatch, "a collection of object")]
    [InlineData("age.foo > 1", RuleErrorCode.UnknownField, "'foo' is not a field of int")]
    [InlineData("country.equalsIgnoreCase()", RuleErrorCode.UnknownMethod, "one string argument")]
    [InlineData("country.equalsIgnoreCase(3)", RuleErrorCode.ExpressionTypeMismatch, "expects a string")]
    [InlineData("orders.count(1) > 1", RuleErrorCode.UnknownMethod, "takes no arguments")]
    [InlineData("orders.sum(1) > 1", RuleErrorCode.UnknownMethod, "takes a lambda")]
    [InlineData("!age", RuleErrorCode.ExpressionTypeMismatch, "'!' needs a condition")]
    [InlineData("-country == \"x\"", RuleErrorCode.ExpressionTypeMismatch, "'-' needs a number")]
    [InlineData("1 && isActive", RuleErrorCode.ExpressionTypeMismatch, "this is a number")]
    [InlineData("country == isActive", RuleErrorCode.ExpressionTypeMismatch, "comparing string with a condition")]
    [InlineData("country > 1", RuleErrorCode.ExpressionTypeMismatch, "compares numbers; this is string")]
    [InlineData("country.equalsIgnoreCase(isActive)", RuleErrorCode.ExpressionTypeMismatch, "expects a string; this is a condition")]
    public void Should_report_type_and_name_problems(string text, RuleErrorCode code, string fragment)
    {
        var analysis = Check(text);
        analysis.IsValid.ShouldBeFalse();
        var problem = analysis.Problems.First(p => !p.IsWarning);
        problem.Code.ShouldBe(code);
        problem.Message.ShouldContain(fragment);
    }

    [Fact]
    public void Should_range_a_division_by_zero_at_the_literal_and_not_also_warn_about_truncation()
    {
        var analysis = Check("age / 0 > 1");
        var problem = analysis.Problems.ShouldHaveSingleItem();
        problem.IsWarning.ShouldBeFalse();
        (problem.Start, problem.End).ShouldBe((6, 7));
    }

    [Fact]
    public void Should_range_an_unknown_lambda_field_at_the_name()
    {
        var problem = Check("orders.sum(o => o.nope) > 1").Problems.Single();
        problem.Start.ShouldBe(18);
        problem.End.ShouldBe(22);
    }

    [Theory]
    [InlineData("country != null")]
    [InlineData("shippedAt == null")]
    [InlineData("orders.any(o => o.status == \"paid\")")]
    [InlineData("country.equalsIgnoreCase(\"se\")")]
    [InlineData("!isActive || points > age")]
    [InlineData("kind == \"Retail\"")]
    [InlineData("maybeKind == null")]
    [InlineData("tier > 0")]
    [InlineData("isActive == true")]
    [InlineData("@flag && isActive")]
    [InlineData("-age < 0")]
    [InlineData("(@minAge + age) + (@vip + creditLimit) > 1")]
    public void Should_accept_null_checks_and_string_and_boolean_forms(string text)
    {
        Check(text).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_warn_on_integer_division_between_two_integral_fields()
    {
        var analysis = Check("age / points > 1");
        analysis.IsValid.ShouldBeTrue();
        analysis.Problems.ShouldHaveSingleItem().Message.ShouldContain("truncates");
    }

    [Fact]
    public void Should_print_every_node_kind_as_its_canonical_text()
    {
        var problems = new List<LeafProblem>();
        var node = LeafParser.Parse("!isActive == true && country == null || -age < @minAge", problems)!;
        LeafChecker.Print(node).ShouldBe("!isActive == true && country == null || -age < @minAge");
    }

    [Fact]
    public void Should_warn_when_a_non_nullable_is_compared_with_null()
    {
        var analysis = Check("age == null");
        analysis.Problems.ShouldHaveSingleItem().IsWarning.ShouldBeTrue();
    }

    private static Type TypeOfIdentifier(string text, string name)
    {
        var analysis = Check(text);
        analysis.IsValid.ShouldBeTrue(string.Join("; ", analysis.Problems.Select(p => p.Message)));
        return analysis.Types.Single(kvp => kvp.Key is Identifier i && i.Name == name).Value;
    }

    [Fact]
    public void Should_read_a_string_converted_enum_as_a_string_and_any_other_as_its_number()
    {
        TypeOfIdentifier("kind == \"Retail\"", "kind").ShouldBe(typeof(string));
        TypeOfIdentifier("tier > 0", "tier").ShouldBe(typeof(int));
        TypeOfIdentifier("maybeKind == null", "maybeKind").ShouldBe(typeof(string));
    }

    [Fact]
    public void Should_name_every_member_when_a_string_enum_literal_is_not_one_of_them()
    {
        var problem = Check("kind == \"Nope\"").Problems.ShouldHaveSingleItem();
        problem.Message.ShouldBe("not one of \"Retail\", \"Wholesale\"");
        (problem.Start, problem.End).ShouldBe((8, 14));
    }

    [Fact]
    public void Should_lift_types_through_a_nullable_path()
    {
        var analysis = Check("orders.sum(o => o.total) > 1");
        var root = analysis.Facts.Single(f => f.Node is Binary);
        root.Type.ShouldBe(typeof(bool));
        analysis.Types[((Binary)root.Node).Left].ShouldBe(typeof(decimal?));
    }

    [Fact]
    public void Should_resolve_mixed_integer_and_number_parameters_to_decimal()
    {
        var analysis = Check("@minAge / @vip > 1");
        analysis.IsValid.ShouldBeTrue();
        analysis.Problems.ShouldHaveSingleItem().IsWarning.ShouldBeTrue();
        analysis.Facts.Single(f => f.Node is ParameterRef p && p.Name == "vip").Type.ShouldBe(typeof(decimal));
        analysis.Facts.Single(f => f.Node is ParameterRef p && p.Name == "minAge").Type.ShouldBe(typeof(decimal));
    }

    [Fact]
    public void Should_not_cascade_a_second_problem_from_a_bad_where_body()
    {
        var analysis = Check("orders.where(o => o.total) > 1");
        analysis.Problems.Count(p => !p.IsWarning).ShouldBe(1);
    }
}
#endif
