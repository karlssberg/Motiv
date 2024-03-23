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
        var antecedent = Spec.Build((bool m) => m)
            .WhenTrue("antecedent satisfied")
            .WhenFalse("antecedent not satisfied")
            .Create();

        var consequent = Spec.Build((bool m) => !m)
            .WhenTrue("consequent satisfied")
            .WhenFalse("consequent not satisfied")
            .Create();

        var sut = antecedent.ElseIf(consequent);

        var result = sut.IsSatisfiedBy(model);

        result.ExplanationTree.Assertions.Humanize().Should().Be(expected);
    }


    [Fact]
    public void Should_describe_the_else_if_spec()
    {
        var antecedent = Spec.Build((bool m) => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .Create();

        var consequent = Spec.Build((bool m) => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .Create();

        var sut = antecedent.ElseIf(consequent);

        sut.Proposition.Statement.Should().Be("antecedent => consequent");
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
        
        var antecedent = Spec.Build((bool m) => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .Create();

        var consequent = Spec.Build((bool m) => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .Create();

        var sut = antecedent.ElseIf(consequent);

        sut.Proposition.Detailed.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "antecedent")]
    [InlineAutoData(false, "not antecedent => consequent")]
    public void Should_describe_consequent_result(bool model, string expected)
    {
        var antecedent = Spec.Build((bool m) => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .Create();

        var consequent = Spec.Build((bool m) => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .Create();

        var sut = antecedent.ElseIf(consequent);
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.Reason.Should().Be(expected);
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
        var antecedent = Spec.Build((bool m) => m)
            .WhenTrue("antecedent")
            .WhenFalse("not antecedent")
            .Create();

        var consequent = Spec.Build((bool m) => !m)
            .WhenTrue("consequent")
            .WhenFalse("not consequent")
            .Create();

        var sut = antecedent.ElseIf(consequent);
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.Detailed.Should().Be(expected);
    }
}