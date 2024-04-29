using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Motiv.Tests;

public class OrElseSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, "not left || not right")]
    [InlineAutoData(false, true, true, "right")]
    [InlineAutoData(true, false, true, "left")]
    [InlineAutoData(true, true, true, "left")]
    public void Should_evaluate_as_a_logical_or_with_short_circuiting(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        string expectedSerialized,
        object model)
    {
        var left = 
            Spec.Build((object _) => leftValue)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right = 
            Spec.Build((object _) => rightValue)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var sut = left.OrElse(right);

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(expectedSatisfied);
        result.Reason.Should().BeEquivalentTo(expectedSerialized);
    }



    [Fact]
    public void Should_not_evaluate_the_right_operand_when_true()
    {
        var left = 
            Spec.Build((object _) => true)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right = 
            Spec.Build(new Func<object, bool>(_ => throw new UnreachableException("Should not be evaluated")))
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var sut = left.OrElse(right);

        Action act = () => sut.IsSatisfiedBy(new object());
        
        act.Should().NotThrow<UnreachableException>();
    }
    
    [Fact]
    public void Should_have_spec_with_propositional_statement()
    {
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var sut = left.OrElse(right);

        sut.Statement.Should().Be("left || right");
    }
    
    [Fact]
    public void Should_describe_in_detail_the_or_else_spec()
    {
        const string expected =
            """
            OR ELSE
                left
                right
            """;
        
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var sut = left.OrElse(right);

        sut.Expression.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "left")]
    [InlineAutoData(false, "right")]
    public void Should_describe_the_result(bool model, string expected)
    {
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var sut = left.OrElse(right);
        
        var act = sut.IsSatisfiedBy(model);

        act.Description.Reason.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, """
                        OR
                            left
                        """)]
    [InlineAutoData(false, """
                        OR
                            right
                        """)]
    public void Should_describe_the_result_in_detail_over_a_single_line_because_operands_are_short(bool model, string expected)
    {
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var sut = left.OrElse(right);
        
        var act = sut.IsSatisfiedBy(model);

        act.Justification.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, """
                                OR
                                    left assertion statement
                                """)]
    [InlineAutoData(false,
                                """
                                OR
                                    right assertion statement
                                """)]
    public void Should_describe_the_result_in_detail_over_multiple_lines_because_operands_are_long(bool model, string expected)
    {
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left assertion statement")
                .WhenFalse("not left assertion statement")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right assertion statement")
                .WhenFalse("not right assertion statement")
                .Create();

        var sut = left.OrElse(right);
        
        var act = sut.IsSatisfiedBy(model);

        act.Justification.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_perform_OrElse_on_specs_with_different_metadata(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        Guid leftTrue,
        Guid leftFalse,
        int  rightTrue,
        int  rightFalse)
    {
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(leftTrue)
                .WhenFalse(leftFalse)
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(rightTrue)
                .WhenFalse(rightFalse)
                .Create("right");

        var sut = left.OrElse(right);
        
        var act = sut.IsSatisfiedBy("");

        act.Satisfied.Should().Be(expectedSatisfied);
    }
    
    [Theory]
    [InlineData(false, false, "!left", "!right")]
    [InlineData(false, true, "right")]
    [InlineData(true, false, "left")]
    [InlineData(true, true, "left")]
    public void Should_perform_OrElse_on_specs_with_different_metadata_and_preserve_assertions(
        bool leftValue,
        bool rightValue,
        params string[] expectedAssertions)
    {
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var sut = left.OrElse(right);
        
        var act = sut.IsSatisfiedBy("");

        act.Assertions.Should().BeEquivalentTo(expectedAssertions);
        act.Metadata.Should().BeEquivalentTo(expectedAssertions);
    }
    
    [Fact]
    public void Should_not_collapse_ORELSE_operators_in_spec_description()
    {
        var first = Spec
            .Build<bool>(_ => true)
            .Create("first");
        
        var second = Spec
            .Build<bool>(_ => true)
            .Create("second");
        
        var third = Spec
            .Build<bool>(_ => true)
            .Create("third");

        var spec = first.OrElse(second).OrElse(third); 
        
        spec.Expression.Should().Be(
            """
            OR ELSE
                first
                second
                third
            """);
    }
}