using AutoFixture.AutoNSubstitute;
using FluentAssertions;
using Humanizer;

namespace Karlssberg.Motiv.Tests;

public class ElseIfSpecTests
{
    [Theory]
    [InlineAutoData(true, "antecedent satisfied")]
    [InlineAutoData(false, "antecedent not satisfied and consequent satisfied")]
    public void Should_return_an_antecedent_result_when_it_is_satisfied(bool model, string expected)
    {
        var antecedent = Spec.Build<bool>(m => m)
            .WhenTrue("antecedent satisfied")
            .WhenFalse("antecedent not satisfied")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .WhenTrue("consequent satisfied")
            .WhenFalse("consequent not satisfied")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);

        var result = sut.IsSatisfiedBy(model);

        result.Explanation.Assertions.Humanize().Should().Be(expected);
    }


    [Fact]
    public void Should_describe_the_else_if_spec()
    {
        var antecedent = Spec.Build<bool>(m => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);

        sut.Proposition.Assertion.Should().Be("antecedent => consequent");
    }
    
    [Fact]
    public void Should_describe_in_detail_the_else_if_spec()
    {
        const string expected =
            """
            antecedent => {
                consequent
            }
            """;
        
        var antecedent = Spec.Build<bool>(m => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);

        sut.Proposition.Detailed.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "antecedent")]
    [InlineAutoData(false, "not antecedent => consequent")]
    public void Should_describe_consequent_result(bool model, string expected)
    {
        var antecedent = Spec.Build<bool>(m => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.Compact.Should().Be(expected);
    }
    
    
    
    [Theory]
    [InlineAutoData(true, "antecedent")]
    [InlineAutoData(false,
        """
        not antecedent => {
            consequent
        }
        """)]
    public void Should_describe_consequent_result_in_detail(bool model, string expected)
    {
        var antecedent = Spec.Build<bool>(m => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .CreateSpec();

        var consequent = Spec.Build<bool>(m => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .CreateSpec();

        var sut = antecedent.ElseIf(consequent);
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.Detailed.Should().Be(expected);
    }
}