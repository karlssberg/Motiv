using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ElseIfSpecTests
{
    [Theory]
    [InlineAutoData(true, "antecedent satisfied")]
    [InlineAutoData(false, "consequent satisfied")]
    public void Should_return_an_antecedent_result_when_it_is_satisfied(bool model, string expected)
    {
        var antecedent = Spec.Build<bool>(m => m)
            .YieldWhenTrue("antecedent satisfied")
            .YieldWhenFalse("antecedent not satisfied")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .YieldWhenTrue("consequent satisfied")
            .YieldWhenFalse("consequent not satisfied")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);

        var result = sut.IsSatisfiedBy(model);

        result.ReasonHierarchy.Select(reason => reason.Description).Should().BeEquivalentTo(expected);
    }


    [Fact]
    public void Should_describe_the_else_if_spec()
    {
        var antecedent = Spec.Build<bool>(m => m)
            .YieldWhenTrue("when true")
            .YieldWhenFalse("when false")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .YieldWhenTrue("when false")
            .YieldWhenFalse("when true")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);

        sut.Description.Should().Be("(when true) => (when false)");
    }
}