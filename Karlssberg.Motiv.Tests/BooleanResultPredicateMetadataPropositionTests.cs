using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class BooleanResultPredicateMetadataPropositionTests
{
    public enum Metadata
    {
        True,
        False
    }
    
    [Theory]
    [InlineData(false, false, true, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineData(false, true, false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [InlineData(true, false, false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [InlineData(true, true, true, "is first true", "is second true", "is third true", "is fourth true")]
    public void Should_replace_the_metadata_with_new_metadata(
        bool model,
        bool other,
        bool expected,
        params string[] expectedAssertion)
    {
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create($"are equal");
        
        var firstSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy(model);

        act.Satisfied.Should().Be(expected);
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(Metadata.True)
                .WhenFalse((_, _) => [Metadata.False])
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
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue(_ => Metadata.True)
                .WhenFalse((_, _) => [Metadata.False])
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
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse((_, _) => [Metadata.False])
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
        string because)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(because, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.IsSatisfiedBy(b))
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => [Metadata.False])
                .Create("propositional statement");
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;
        
        var act = spec.IsSatisfiedBy(model);
        
        act.Reason.Should().Be(expectedReason);
    }
}