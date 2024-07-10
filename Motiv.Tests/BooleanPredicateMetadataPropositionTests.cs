namespace Motiv.Tests;

public class BooleanPredicateMetadataPropositionTests
{
    public enum Metadata
    {
        True,
        False
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_satisfy_spec_when_replacing_the_metadata_with_new_metadata(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineData(false, true, "¬is first true", "¬is second true", "¬is third true", "¬is fourth true")]
    [InlineData(true, false, "¬is first true", "¬is second true", "¬is third true", "¬is fourth true")]
    [InlineData(true, true, "is first true", "is second true", "is third true", "is fourth true")]
    public void Should_use_proposition_statement_when_generating_assertions_for_metadata_propositions(
        bool model,
        bool other,
        params string[] expectedAssertion)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => m == other)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_false_metadata_is_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        string because)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(because, 2));

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrueYield(_ => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrueYield(_ => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange

        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_as_a_policy(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange

        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var result = spec.Execute(model);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be(expectedMetadata);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_as_a_policy_using_metadata_callbacks(
        bool model,
        Metadata expectedValue)
    {
        // Arrange

        var spec =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");


        var result = spec.Execute(model);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_from_the_MetadataTier(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.MetadataTier.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_from_the_MetadataTier_when_true_assertion_uses_a_single_parameter_callback(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.MetadataTier.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_from_the_MetadataTier_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.MetadataTier.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrueYield(_ => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrueYield(_ => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(true, Metadata.True)]
    [InlineData(false, Metadata.False)]
    public void Should_yield_the_appropriate_metadata_from_the_MetadataTier_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection(
        bool model,
        Metadata expectedMetadata)
    {
        // Arrange
        var withFalseAsScalar =
            Spec.Build((bool m) => m)
                .WhenTrueYield(_ => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool m) => m)
                .WhenTrueYield(_ => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var spec = withFalseAsScalar & withFalseAsParameterCallback;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.MetadataTier.Metadata;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }
}
