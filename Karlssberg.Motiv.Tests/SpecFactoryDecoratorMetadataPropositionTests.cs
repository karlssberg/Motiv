using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class SpecFactoryDecoratorMetadataPropositionTests
{
    public enum Metadata
    {
        True,
        False
    }
    
    [InlineData(true, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineData(false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [Theory]
    public void Should_replace_the_assertion_with_new_assertion(
        bool isSatisfied,
        params string[] expected)
    {
        var underlying = Spec
            .Build((string _) => isSatisfied)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create("is underlying true");

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expected);
    }
    
    [InlineData(true, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineData(false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [Theory]
    public void Should_replace_the_assertion_with_new_assertion_for_parameterless_spec_factories(
        bool isSatisfied,
        params string[] expected)
    {
        var underlying = Spec
            .Build((string _) => isSatisfied)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create("is underlying true");

        var firstSpec = Spec
            .Build(() => underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build(() => underlying)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build(() => underlying)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build(() => underlying)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expected);
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
            Spec.Build(underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
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
    public void Should_use_the_propositional_statement_in_the_reason_for_parameterless_spec_factories(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(() => underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue(Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(() => underlying)
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
            Spec.Build(underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_parameterless_spec_factories(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(() => underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(() => underlying)
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
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_for_parameterless_spec_factories(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(() => underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue((_, _) => Metadata.True)
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(() => underlying)
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
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
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
    
    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "!propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_parameterless_spec_factories(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(() => underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(() => underlying)
                .WhenTrue((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(() => underlying)
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