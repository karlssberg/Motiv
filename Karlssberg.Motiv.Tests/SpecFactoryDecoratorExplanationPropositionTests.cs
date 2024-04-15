using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class SpecFactoryDecoratorExplanationPropositionTests
{
    [Theory]
    [InlineData(true, "true - A", "true + model - B", "true - C", "true + model - D")]
    [InlineData(false, "false - A", "false - B", "false + model - C", "false + model - D")]
public void Should_replace_the_assertions_with_new_assertions(
        bool isSatisfied,
        params string[] expected)
    {
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue("true - A")
            .WhenFalse("false - A")
            .Create();

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - B")
            .WhenFalse("false - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue("true - C")
            .WhenFalse(model => $"false + {model} - C")
            .Create();

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue(model => $"true + {model} - D")
            .WhenFalse(model => $"false + {model} - D")
            .Create("true + model - D");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expected);
        act.MetadataTree.Should().BeEquivalentTo(expected);
    }
    
    [InlineAutoData(true, "true - A", "true + model - B", "true - C", "true + model - D")]
    [InlineAutoData(false, "false - A", "false - B", "false + model - C", "false + model - D")]
    [Theory]
    public void Should_replace_the_assertions_collection_with_new_assertions_collection(
        bool isSatisfied,
        params string[] expected)
    {
        var underlying = Spec
            .Build((string _) => isSatisfied)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue("true - A")
            .WhenFalse("false - A")
            .Create();

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue((m, _) => [$"true + {m} - B"])
            .WhenFalse("false - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue("true - C")
            .WhenFalse((m, _)  => [$"false + {m} - C"])
            .Create();

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue((m, _)  => [$"true + {m} - D"])
            .WhenFalse((m, _)  => [$"false + {m} - D"])
            .Create("true + model - D");

        var sut = !(!firstSpec & !secondSpec & !thirdSpec & !fourthSpec);

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expected);
        act.MetadataTree.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "True True True")]
    [InlineAutoData(false, "False False False")]
    public void Should_map_existing_assertions_to_new_metadata(bool model, string expected)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is underlying true");

        var sut = Spec
            .Build(underlying)
            .WhenTrue((boolModel, result) => $"{true.ToString()} {boolModel} {result}")
            .WhenFalse((boolModel, result) => $"{false.ToString()} {boolModel} {result}")
            .Create("is true");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineAutoData(true, "True", "is true")]
    [InlineAutoData(false, "False", "!is true")]
    public void Should_map_underlying_true_assertions_to_new_ones(bool model, string expectedAssertion, string expectedReason)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is underlying true");

        var sut = Spec
            .Build(underlying)
            .WhenTrue((boolModel, result) => result.Assertions.Append(boolModel.ToString()))
            .WhenFalse("False")
            .Create("is true");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineAutoData(true, "True", "is true")]
    [InlineAutoData(false, "False", "!is true")]
    public void Should_map_underlying_false_assertions_to_new_ones(bool model, string expectedAssertion, string expectedReason)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is underlying true");

        var sut = Spec
            .Build(underlying)
            .WhenTrue("True")
            .WhenFalse((boolModel, result) => result.Assertions.Append(boolModel.ToString()))
            .Create("is true");
        
        var act = sut.IsSatisfiedBy(model);
        
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
        act.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(true, "true assertion")]
    [InlineData(false, "false assertion")]
    public void Should_harvest_propositionStatement_from_assertion(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create();
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create();
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
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
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue("true assertion")
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
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    { 
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 5));
        
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");
        
        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => ["false assertion"])
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollectionWithoutCustomStatement =
            Spec.Build(underlying)
                .WhenTrue("propositional statement")
                .WhenFalse((_, _) => ["false assertion"])
                .Create();
        
        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection &
                   withFalseAsTwoParameterCallbackThatReturnsACollectionWithoutCustomStatement;
        
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
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
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
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");
        
        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrue((_, _) => ["true assertion"])
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");
        
        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
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