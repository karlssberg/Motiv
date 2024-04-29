using FluentAssertions;

namespace Motiv.Tests;

public class BooleanResultPredicateExplanationPropositionTests
{
    [Theory]
    [InlineData(false, false, true, "underlying is true", "first is true", "second is true", "third is true", "fourth is true")]
    [InlineData(false, true, false, "underlying is false", "first is false", "second is false", "third is false", "fourth is false")]
    [InlineData(true, false, false, "underlying is false", "first is false", "second is false", "third is false", "fourth is false")]
    [InlineData(true, true, true, "underlying is true", "first is true", "second is true", "third is true", "fourth is true")]
    public void Should_replace_the_assertion_with_new_assertion(
        bool model,
        bool other,
        bool expected,
        string expectedRootAssertion,
        params string[] expectedAssertions)
    {
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

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy(model);
        
        act.Satisfied.Should().Be(expected);
        act.RootAssertions.Should().BeEquivalentTo(expectedRootAssertion);
        act.Assertions.Should().BeEquivalentTo(expectedAssertions);
    }
    
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        string expectedReasonStatement)
    { 
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
        
        var act = spec.IsSatisfiedBy(model);
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedReasonStatement)
    { 
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
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
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
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
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
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        string expectedReasonStatement)
    { 
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
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
}