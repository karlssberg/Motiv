using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafParserTests
{
    private static (LeafNode? Node, List<LeafProblem> Problems) Parse(string text)
    {
        var problems = new List<LeafProblem>();
        return (LeafParser.Parse(text, problems), problems);
    }

    [Fact]
    public void Should_parse_precedence_from_comparison_down_to_multiplication()
    {
        var (node, problems) = Parse("age * 12 + 1 >= @min && isActive");

        problems.ShouldBeEmpty();
        var and = node.ShouldBeOfType<Binary>();
        and.Operator.ShouldBe("&&");
        var cmp = and.Left.ShouldBeOfType<Binary>();
        cmp.Operator.ShouldBe(">=");
        var add = cmp.Left.ShouldBeOfType<Binary>();
        add.Operator.ShouldBe("+");
        add.Left.ShouldBeOfType<Binary>().Operator.ShouldBe("*");
        cmp.Right.ShouldBeOfType<ParameterRef>().Name.ShouldBe("min");
        and.Right.ShouldBeOfType<Identifier>().Name.ShouldBe("isActive");
        and.Start.ShouldBe(0);
        and.End.ShouldBe(32);
    }

    [Fact]
    public void Should_parse_a_method_chain_with_lambdas()
    {
        var (node, problems) = Parse("orders.where(o => o.status == \"paid\").sum(o => o.total) > 1000");

        problems.ShouldBeEmpty();
        var cmp = node.ShouldBeOfType<Binary>();
        var sum = cmp.Left.ShouldBeOfType<MethodCall>();
        sum.Method.ShouldBe("sum");
        sum.MethodStart.ShouldBe(38);
        var lambda = sum.Arguments.ShouldHaveSingleItem().ShouldBeOfType<Lambda>();
        lambda.Parameter.ShouldBe("o");
        lambda.Body.ShouldBeOfType<MemberAccess>().Name.ShouldBe("total");
        var where = sum.Target.ShouldBeOfType<MethodCall>();
        where.Method.ShouldBe("where");
        where.Target.ShouldBeOfType<Identifier>().Name.ShouldBe("orders");
        cmp.Right.ShouldBeOfType<NumberLiteral>().Text.ShouldBe("1000");
    }

    [Fact]
    public void Should_parse_unary_and_parentheses()
    {
        var (node, _) = Parse("!(a || b) && -x < 0");
        var and = node.ShouldBeOfType<Binary>();
        and.Left.ShouldBeOfType<Unary>().Operator.ShouldBe("!");
        and.Right.ShouldBeOfType<Binary>().Left.ShouldBeOfType<Unary>().Operator.ShouldBe("-");
    }

    [Theory]
    [InlineData("", "empty expression", 0, 0)]
    [InlineData("age >", "unexpected end of expression", 5, 5)]
    [InlineData("age > > 1", "unexpected '>'", 6, 7)]
    [InlineData("orders.", "expected a field or method name after '.'", 7, 7)]
    [InlineData("a == b == c", "unexpected '=='", 7, 9)]
    public void Should_report_syntax_errors_at_their_range(string text, string message, int start, int end)
    {
        var (node, problems) = Parse(text);
        node.ShouldBeNull();
        var problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(RuleErrorCode.InvalidExpression);
        problem.Message.ShouldBe(message);
        problem.Start.ShouldBe(start);
        problem.End.ShouldBe(end);
    }
}
