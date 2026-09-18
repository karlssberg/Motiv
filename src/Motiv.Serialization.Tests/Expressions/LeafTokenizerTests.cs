using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafTokenizerTests
{
    private static (IReadOnlyList<LeafToken> Tokens, List<LeafProblem> Problems) Tokenize(string text)
    {
        var problems = new List<LeafProblem>();
        return (LeafTokenizer.Tokenize(text, problems), problems);
    }

    [Fact]
    public void Should_tokenize_a_filtered_aggregate_comparison()
    {
        var (tokens, problems) = Tokenize("orders.where(o => o.status == \"paid\").sum(o => o.total) > @vip");

        problems.ShouldBeEmpty();
        tokens.Select(t => t.Kind).ShouldBe([
            LeafTokenKind.Identifier, LeafTokenKind.Punctuation, LeafTokenKind.Identifier, LeafTokenKind.Punctuation,
            LeafTokenKind.Identifier, LeafTokenKind.Operator, LeafTokenKind.Identifier, LeafTokenKind.Punctuation,
            LeafTokenKind.Identifier, LeafTokenKind.Operator, LeafTokenKind.String, LeafTokenKind.Punctuation,
            LeafTokenKind.Punctuation, LeafTokenKind.Identifier, LeafTokenKind.Punctuation, LeafTokenKind.Identifier,
            LeafTokenKind.Operator, LeafTokenKind.Identifier, LeafTokenKind.Punctuation, LeafTokenKind.Identifier,
            LeafTokenKind.Punctuation, LeafTokenKind.Operator, LeafTokenKind.Parameter, LeafTokenKind.End]);
        tokens[10].Text.ShouldBe("\"paid\"");
        tokens[22].Text.ShouldBe("@vip");
        tokens[22].Start.ShouldBe(58);
    }

    [Theory]
    [InlineData("null", LeafTokenKind.Keyword)]
    [InlineData("true", LeafTokenKind.Keyword)]
    [InlineData("1.5", LeafTokenKind.Number)]
    [InlineData("=>", LeafTokenKind.Operator)]
    [InlineData("!=", LeafTokenKind.Operator)]
    public void Should_classify_single_tokens(string text, LeafTokenKind kind)
    {
        var (tokens, _) = Tokenize(text);
        tokens[0].Kind.ShouldBe(kind);
        tokens[0].Text.ShouldBe(text);
    }

    [Fact]
    public void Should_report_an_unterminated_string_at_its_range()
    {
        var (_, problems) = Tokenize("country == \"SE");
        var problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(RuleErrorCode.InvalidExpression);
        problem.Start.ShouldBe(11);
        problem.End.ShouldBe(14);
    }

    [Fact]
    public void Should_report_an_unrecognised_character()
    {
        var (_, problems) = Tokenize("age # 3");
        var problem = problems.ShouldHaveSingleItem();
        problem.Message.ShouldContain("#");
        problem.Start.ShouldBe(4);
    }
}
