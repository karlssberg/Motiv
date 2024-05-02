using FluentAssertions;

namespace Motiv.Tests;

public class BooleanResultPredicateExplanationPropositionTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_boolean_predicate(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");
        
        var firstSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, "underlying is true")]
    [InlineData(false, true, "underlying is false")]
    [InlineData(true, false, "underlying is false")]
    [InlineData(true, true, "underlying is true")]
    public void Should_provide_root_assertions(
        bool model,
        bool other,
        string expectedRootAssertion)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");
        
        var firstSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.RootAssertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedRootAssertion);
    }
    
    [Theory]
    [InlineData(false, false, "first is true", "second is true", "third is true", "fourth is true")]
    [InlineData(false, true, "first is false", "second is false", "third is false", "fourth is false")]
    [InlineData(true, false, "first is false", "second is false", "third is false", "fourth is false")]
    [InlineData(true, true, "first is true", "second is true", "third is true", "fourth is true")]
    public void Should_replace_the_assertion_with_new_assertion(
        bool model,
        bool other,
        params string[] expectedAssertions)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");
        
        var firstSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }
    
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Should_a_reason_from_assertions(
        bool model,
        string expectedReasonStatement)
    { 
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true")
                .WhenFalse("false")
                .Create();
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true")
                .WhenFalse(_ => "false")
                .Create();
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true")
                .WhenFalse((_, _) => "false")
                .Create();

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback; 
        
        var result = spec.IsSatisfiedBy(model);
        
        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedReasonStatement)
    { 
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 5));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollectionAndNoCustomName =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue("propositional statement")
                .WhenFalse((_, _) => ["false assertion"])
                .Create();

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection &
                   withFalseAsTwoParameterCallbackThatReturnsACollectionAndNoCustomName;
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var result = spec.IsSatisfiedBy(model);

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
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
        
    }
    
    [Fact]
    public void Should_describe_a_boolean_result_spec()
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var spec = 
            Spec.Build((bool model) => underlying.IsSatisfiedBy(model))
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create("is model true");
           
        // Act
        var act = spec.Description.Statement;

        // Assert
        act.Should().Be("is model true");
    }
}