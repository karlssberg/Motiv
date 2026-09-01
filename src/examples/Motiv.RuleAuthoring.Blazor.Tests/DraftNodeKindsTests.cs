using Motiv.RuleAuthoring.Blazor.Authoring;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

public class DraftNodeKindsTests
{
    /// <remarks>
    /// <see cref="DraftNodeKinds.Operators" /> fills the editor's kind dropdown, so a kind missing
    /// from it is a composition the sample silently cannot author.
    /// </remarks>
    [Fact]
    public void Offers_every_kind_except_the_spec_node()
    {
        DraftNodeKinds.Operators.ShouldBe(
            Enum.GetValues<DraftNodeKind>().Except([DraftNodeKind.Spec]),
            ignoreOrder: true);
    }

    [Theory]
    [InlineData(DraftNodeKind.Not, "not")]
    [InlineData(DraftNodeKind.And, "and")]
    [InlineData(DraftNodeKind.Or, "or")]
    [InlineData(DraftNodeKind.XOr, "xor")]
    [InlineData(DraftNodeKind.AndAlso, "andAlso")]
    [InlineData(DraftNodeKind.OrElse, "orElse")]
    public void Writes_each_operator_under_its_schema_keyword(DraftNodeKind kind, string keyword)
    {
        DraftNodeKinds.Keyword(kind).ShouldBe(keyword);
    }

    [Fact]
    public void Refuses_a_keyword_for_a_spec_node()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DraftNodeKinds.Keyword(DraftNodeKind.Spec));
    }
}
