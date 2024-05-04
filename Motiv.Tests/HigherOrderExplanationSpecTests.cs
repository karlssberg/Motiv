namespace Motiv.Tests;

public class HigherOrderExplanationSpecTests
{
    [Theory]
    [InlineData(1, 3, 5, 7, "is not a pair of even numbers")]
    [InlineData(1, 3, 5, 8, "is not a pair of even numbers")]
    [InlineData(1, 3, 6, 8, "is a pair of even numbers")]
    [InlineData(1, 3, 5, 9, "is not a pair of even numbers")]
    public void Should_supplant_metadata_from_a_higher_order_spec(int first, int second, int third, int fourth, string expected)
    {
        // Arrange
        var underlyingSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(i => $"{i} is even")
                .WhenFalse(i => $"{i} is odd")
                .Create("is even spec");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNSatisfied(2)
                .WhenTrue("is a pair of even numbers")
                .WhenFalse("is not a pair of even numbers")
                .Create();

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Explanation.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Should_preserve_the_description_of_the_underlying_()
    {
        // Arrange
        var underlyingSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue("is even")
                .WhenFalse("is odd")
                .Create("is even spec");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNSatisfied(2)
                .WhenTrue("is a pair of even numbers")
                .WhenFalse("is not a pair of even numbers")
                .Create();

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be("is a pair of even numbers");
    }

    [Theory]
    [InlineData(true, true, true, "third all true")]
    [InlineData(true, true, false, "third all false")]
    [InlineData(true, false, true, "third all false")]
    [InlineData(true, false, false, "third all false")]
    [InlineData(false, true, true, "third all false")]
    [InlineData(false, true, false, "third all false")]
    [InlineData(false, false, true, "third all false")]
    [InlineData(false, false, false, "third all false")]
    public void Should_only_yield_the_most_recent_when_multiple_yields_are_chained(bool first, bool second, bool third, string expected)
    {
        // Arrange
        var underlying =
            Spec.Build((bool b) => b)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create();

        var firstSpec =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("first all true")
                .WhenFalse("first all false")
                .Create();
            
        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue("second all true")
                .WhenFalse("second all false")
                .Create();
            
        var spec =
            Spec.Build(secondSpec)
                .WhenTrue("third all true")
                .WhenFalse("third all false")
                .Create();

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Explanation.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(true, true, true, "is true")]
    [InlineData(true, true, false, "is false")]
    [InlineData(true, false, true, "is false")]
    [InlineData(true, false, false, "is false")]
    [InlineData(false, true, true, "is false")]
    [InlineData(false, true, false, "is false")]
    [InlineData(false, false, true, "is false")]
    [InlineData(false, false, false, "is false")]
    public void Should_yield_the_most_deeply_nested_reason_when_requested(bool first, bool second, bool third, string expected)
    {
        // Arrange
        var underlyingSpec =
            Spec.Build<bool>(b => b)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create();

        var firstSpec =
            Spec.Build(underlyingSpec).AsAllSatisfied()
                .WhenTrue("first true")
                .WhenFalse("first false")
                .Create("all even");
            
        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue("second true")
                .WhenFalse("second false")
                .Create();
            
        var spec =
            Spec.Build(secondSpec)
                .WhenTrue("third true")
                .WhenFalse("third false")
                .Create("all even");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.GetRootAssertions();
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(2, 4, 6, 8, true)]
    [InlineData(2, 4, 6, 9, false)]
    [InlineData(1, 4, 6, 9, false)]
    [InlineData(1, 3, 6, 9, false)]
    [InlineData(1, 3, 5, 9, false)]
    public void Should_satisfy_regular_true_assertion_yield_to_be_used_with_a_higher_order_yield_of_false_assertions(
        int first, 
        int second, 
        int third,
        int fourth,
        bool expected)
    {
        // Arrange
        var underlyingSpec =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(i => $"{i} is even")
                .WhenFalse(i => $"{i} is odd")
                .Create("is even spec");

        var spec =
            Spec.Build(underlyingSpec)
                .AsAllSatisfied()
                .WhenTrue("all even")
                .WhenFalse(results =>
                {
                    var serializedModels = results.CausalModels.Serialize();
                    var modelCount = results.CausalModels.Count;
                    var isOrAre = modelCount == 1 ? "is" : "are";
                    
                    return $"not all even: {serializedModels} {isOrAre} odd";
                })
                .Create();

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    
    [Theory]
    [InlineData(2, 4, 6, 8, "all even")]
    [InlineData(2, 4, 6, 9, "not all even: 9 is odd")]
    [InlineData(1, 4, 6, 9, "not all even: 1 and 9 are odd")]
    [InlineData(1, 3, 6, 9, "not all even: 1, 3, and 9 are odd")]
    [InlineData(1, 3, 5, 9, "not all even: 1, 3, 5, and 9 are odd")]
    public void Should_assert_regular_true_assertion_yield_to_be_used_with_a_higher_order_yield_of_false_assertions(
        int first, 
        int second, 
        int third,
        int fourth,
        string expectedReason)
    {
        // Arrange
        var underlyingSpec =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(i => $"{i} is even")
                .WhenFalse(i => $"{i} is odd")
                .Create("is even spec");

        var spec =
            Spec.Build(underlyingSpec)
                .AsAllSatisfied()
                .WhenTrue("all even")
                .WhenFalse(results =>
                {
                    var serializedModels = results.CausalModels.Serialize();
                    var modelCount = results.CausalModels.Count;
                    var isOrAre = modelCount == 1 ? "is" : "are";
                    
                    return $"not all even: {serializedModels} {isOrAre} odd";
                })
                .Create();

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedReason);
    }
    
    
    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create();
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create();

        var spec = withFalseAsScalar & withFalseAsParameterCallback;
        
        var result = spec.IsSatisfiedBy([model]);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "true assertion",  "true assertion", "propositional statement")]
    [InlineData(false, "false assertion", "!true assertion", "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedAssertion,
        string expectedImplicitAssertion,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ",
            expectedAssertion,
            expectedAssertion,
            expectedReasonStatement,
            expectedImplicitAssertion);
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("true assertion")
                .WhenFalseYield(_ => ["false assertion"])
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollectionWithImpliedName =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue("true assertion")
                .WhenFalseYield(_ => ["false assertion"])
                .Create();
        
        var spec = withFalseAsScalar &
                   withFalseAsCallback &
                   withFalseAsCallbackThatReturnsACollection &
                   withFalseAsTwoParameterCallbackThatReturnsACollectionWithImpliedName;

        var result = spec.IsSatisfiedBy([model]);
        
        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "true assertion", "propositional statement")]
    [InlineData(false, "false assertion", "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedAssertion,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ",
            expectedAssertion,
            expectedAssertion,
            expectedAssertion,
            expectedReasonStatement);
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalseYield(_ => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);
        
        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "true assertion", "propositional statement")]
    [InlineData(false, "false assertion", "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedAssertion,
        string expectedReasonStatement)
    {
        // Arrange 
        var expectedReason = string.Join(" & ", 
            expectedAssertion,
            expectedAssertion,
            expectedAssertion,
            expectedReasonStatement);
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue(_ => "true assertion")
                .WhenFalseYield(_ => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);
        
        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrueYield(_ => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsCallback =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrueYield(_ => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        
        var withFalseAsCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrueYield(_ => ["true assertion"])
                .WhenFalseYield(_ => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsCallback &
                   withFalseAsCallbackThatReturnsACollection;

        var result = spec.IsSatisfiedBy([model]);
        
        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
}