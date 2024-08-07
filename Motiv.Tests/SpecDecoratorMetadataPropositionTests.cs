namespace Motiv.Tests;

public class SpecDecoratorMetadataPropositionTests
{
    public enum Metadata
    {
        True,
        False
    }

    [Theory]
    [InlineData(true, "is first true", "is second true", "is third true", "is fourth true", "is fifth true", "is sixth true", "is seventh true")]
    [InlineData(false, "¬is first true", "¬is second true", "¬is third true", "¬is fourth true", "¬is fifth true", "¬is sixth true", "¬is seventh true")]
    public void Should_replace_the_assertion_with_new_assertion_for_policies(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        PolicyBase<string,int> underlying = Spec
            .Build<string>(_ => isSatisfied)
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue(600)
            .WhenFalse((_, _) => -600)
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 700)
            .WhenFalse(-700)
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 800)
            .WhenFalse((_, _) => -800)
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(true, "is first true", "is second true", "is third true", "is fourth true", "is fifth true", "is sixth true", "is seventh true")]
    [InlineData(false, "¬is first true", "¬is second true", "¬is third true", "¬is fourth true", "¬is fifth true", "¬is sixth true", "¬is seventh true")]
    public void Should_replace_the_assertion_with_new_assertion_for_specs(
        bool isSatisfied,
        params string[] expected)
    {
        // Arrange
        SpecBase<string,int> underlying = Spec
            .Build<string>(_ => isSatisfied)
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue(600)
            .WhenFalse((_, _) => -600)
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 700)
            .WhenFalse(-700)
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 800)
            .WhenFalse((_, _) => -800)
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineData(true, 200, 300, 400, 500, 600, 700, 800)]
    [InlineData(false, -200, -300, -400, -500, -600, -700, -800)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_for_policies(
        bool isSatisfied,
        params int[] expected)
    {
        // Arrange
        PolicyBase<string,int> underlying = Spec
            .Build<string>(_ => isSatisfied)
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue(600)
            .WhenFalse((_, _) => -600)
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 700)
            .WhenFalse(-700)
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 800)
            .WhenFalse((_, _) => -800)
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineData(true, 200, 300, 400, 500, 600, 700, 800)]
    [InlineData(false, -200, -300, -400, -500, -600, -700, -800)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_for_specs(
        bool isSatisfied,
        params int[] expected)
    {
        // Arrange
        SpecBase<string,int> underlying = Spec
            .Build<string>(_ => isSatisfied)
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

        var fifthSpec = Spec
            .Build(underlying)
            .WhenTrue(600)
            .WhenFalse((_, _) => -600)
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 700)
            .WhenFalse(-700)
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 800)
            .WhenFalse((_, _) => -800)
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [InlineAutoData(true, 1, 3, 5, 7)]
    [InlineAutoData(false, 2, 4, 6, 8)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_type_for_policies(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD,
        string trueReason,
        string falseReason)
    {
        // Arrange
        int[] expectation = [expectedA, expectedB, expectedC, expectedD];
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue(trueReason)
            .WhenFalse(falseReason)
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(1)
            .WhenFalse(2)
            .Create("first spec");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 3)
            .WhenFalse(4)
            .Create("second spec");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(5)
            .WhenFalse(_ => 6)
            .Create("third spec");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 7)
            .WhenFalse(_ => 8)
            .Create("fourth spec");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        result.GetRootAssertions().Should().BeEquivalentTo(result.Satisfied
            ? trueReason
            : falseReason);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expectation);
    }

    [InlineAutoData(true, 1, 3, 5, 7)]
    [InlineAutoData(false, 2, 4, 6, 8)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_type_for_specs(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD,
        string trueReason,
        string falseReason)
    {
        // Arrange
        int[] expectation = [expectedA, expectedB, expectedC, expectedD];
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue(trueReason)
            .WhenFalse(falseReason)
            .Create();

        var firstSpec = Spec
            .Build(underlying)
            .WhenTrue(1)
            .WhenFalse(2)
            .Create("first spec");

        var secondSpec = Spec
            .Build(underlying)
            .WhenTrue(_ => 3)
            .WhenFalse(4)
            .Create("second spec");

        var thirdSpec = Spec
            .Build(underlying)
            .WhenTrue(5)
            .WhenFalse(_ => 6)
            .Create("third spec");

        var fourthSpec = Spec
            .Build(underlying)
            .WhenTrue((_, _) => 7)
            .WhenFalse(_ => 8)
            .Create("fourth spec");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.IsSatisfiedBy("model");

        result.GetRootAssertions().Should().BeEquivalentTo(result.Satisfied
            ? trueReason
            : falseReason);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_policies()
    {
        // Arrange
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(Metadata.True)
            .WhenFalse(Metadata.False)
            .Create("is on");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([Metadata.True]);
    }

    [Fact]
    public void Should_accept_assertion_when_true_for_specs()
    {
        // Arrange
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(Metadata.True)
            .WhenFalse(Metadata.False)
            .Create("is on");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([Metadata.True]);
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should_accept_single_parameter_metadata_factory_for_policies(bool model, Metadata expected)
    {
        // Arrange
        PolicyBase<bool,string> underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(_ => Metadata.True)
            .WhenFalse(_ => Metadata.False)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected.ToEnumerable());
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should_accept_single_parameter_metadata_factor_for_specs(bool model, Metadata expected)
    {
        // Arrange
        SpecBase<bool,string> underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(_ => Metadata.True)
            .WhenFalse(_ => Metadata.False)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expected.ToEnumerable());
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should_provide_a_value_property_after_accepting_single_parameter_metadata_factory_for_policies(bool model, Metadata expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(_ => Metadata.True)
            .WhenFalse(_ => Metadata.False)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should__double_parameter_metadata_factory_for_policies(bool model, Metadata expected)
    {
        // Arrange
        PolicyBase<bool,Metadata> underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(Metadata.True)
            .WhenFalse(Metadata.False)
            .Create("is on");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((m, r) => (Model: m, Metadata: r.Values))
            .WhenFalse((m, r) => (Model: m, Metadata: r.Values))
            .Create("is outer on");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(model, expected.ToEnumerable())]);
    }

    [Theory]
    [InlineAutoData(true,  Metadata.True)]
    [InlineAutoData(false, Metadata.False)]
    public void Should__double_parameter_metadata_factory_for_specs(bool model, Metadata expected)
    {
        // Arrange
        SpecBase<bool,Metadata> underlying = Spec
            .Build((bool m) => m)
            .WhenTrue(Metadata.True)
            .WhenFalse(Metadata.False)
            .Create("is on");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((m, r) => (Model: m, Metadata: r.Values))
            .WhenFalse((m, r) => (Model: m, Metadata: r.Values))
            .Create("is outer on");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(model, expected.ToEnumerable())]);
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_assertions_when_true_for_policies(
            string model)
    {
        // Arrange
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Values.Select(meta => $"{trueModel} - {meta}").First())
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_assertions_when_true_for_specs(
            string model)
    {
        // Arrange
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => true)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue((trueModel, result) => result.Values.Select(meta => $"{trueModel} - {meta}").First())
            .WhenFalse("false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying true");
    }

    [Theory]
    [AutoData]
    public void Should_accept_metadata_when_true_for_policies(
        Guid isTrueMetadata,
        Guid isFalseMetadata)
    {
        // Arrange
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([isTrueMetadata]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_metadata_when_true_for_specs(
        Guid isTrueMetadata,
        Guid isFalseMetadata)
    {
        // Arrange
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => true)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([isTrueMetadata]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_true_for_policies(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isFalseMetadata)
    {
        // Arrange
        PolicyBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((model, result) => result.Values.Select(meta => (model, meta)).First())
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_true_for_specs(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isFalseMetadata)
    {
        // Arrange
        SpecBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((model, result) => result.Values.Select(meta => (model, meta)).First())
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_true_for_for_policies(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isFalseMetadata)
    {
        // Arrange
        PolicyBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((model, result) => result.Values.Select(meta => (model, meta)).First())
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be((guidModel, underlyingTrueGuid));
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_and_return_policy_when_true_for_specs(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isFalseMetadata)
    {
        // Arrange
        SpecBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue((model, result) => result.Values.Select(meta => (model, meta)).First())
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be((guidModel, underlyingTrueGuid));
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_true_for_spec_decorators(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        var underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((model, result) => result.Values.Select(meta => (model, meta)))
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_true_for_policies(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        PolicyBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((model, result) => result.Values.Select(meta => (model, meta)))
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_true_for_specs(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        SpecBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => true)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((model, result) => result.Values.Select(meta => (model, meta)))
            .WhenFalse(model => (model, isFalseMetadata))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingTrueGuid)]);
    }

    [Fact]
    public void Should_accept_assertion_when_false_for_policies()
    {
        // Arrange
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo("false");
    }

    [Fact]
    public void Should_accept_assertion_when_false_for_specs()
    {
        // Arrange
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse("false")
            .Create();

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo("false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_assertions_when_false_for_policies(
            string model)
    {
        // Arrange
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseResult, result) => result.Values.Select(meta => $"{falseResult} - {meta}").First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_assertions_when_false_for_specs(
            string model)
    {
        // Arrange
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => false)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create();

        var spec = Spec
            .Build(underlying)
            .WhenTrue("true")
            .WhenFalse((falseResult, result) => result.Values.Select(meta => $"{falseResult} - {meta}").First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo($"{model} - underlying false");
    }

    [Theory]
    [AutoData]
    public void Should_accept_metadata_when_false_for_polices(
        Guid isTrueMetadata,
        Guid isFalseMetadata)
    {
        // Arrange
        PolicyBase<string,string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([isFalseMetadata]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_metadata_when_false_for_specs(
        Guid isTrueMetadata,
        Guid isFalseMetadata)
    {
        // Arrange
        SpecBase<string,string> underlying = Spec
            .Build<string>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(isFalseMetadata)
            .Create("is true");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([isFalseMetadata]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_metadata_factory_when_false_for_policies(
        Guid guidModel,
        Guid isTrueMetadata)
    {
        // Arrange
        PolicyBase<Guid,string> underlying = Spec
            .Build<Guid>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(model => model)
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([guidModel]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_single_parameter_metadata_factory_when_false_for_specs(
        Guid guidModel,
        Guid isTrueMetadata)
    {
        // Arrange
        SpecBase<Guid,string> underlying = Spec
            .Build<Guid>(_ => false)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(isTrueMetadata)
            .WhenFalse(model => model)
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([guidModel]);
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_false_for_policies(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isTrueMetadata)
    {
        // Arrange
        PolicyBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => false)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => (model, isTrueMetadata))
            .WhenFalse((model, result) => result.Values.Select(meta => (model, meta)).First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be((guidModel, underlyingFalseGuid));
    }

    [Theory]
    [AutoData]
    public void Should_accept_double_parameter_metadata_factory_when_false_for_specs(
        Guid underlyingTrueGuid,
        Guid underlyingFalseGuid,
        Guid guidModel,
        Guid isTrueMetadata)
    {
        // Arrange
        SpecBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => false)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => (model, isTrueMetadata))
            .WhenFalse((model, result) => result.Values.Select(meta => (model, meta)).First())
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Value;

        // Assert
        act.Should().Be((guidModel, underlyingFalseGuid));
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_false_for_policies(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        PolicyBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => false)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => (model, isFalseMetadata))
            .WhenFalseYield((model, result) => result.Values.Select(meta => (model, meta)))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingFalseGuid)]);
    }

    [Theory]
    [AutoData]
    public void
        Should_accept_double_parameter_metadata_factory_that_returns_a_collection_of_metadata_when_false_for_specs(
            Guid underlyingTrueGuid,
            Guid underlyingFalseGuid,
            Guid guidModel,
            Guid isFalseMetadata)
    {
        // Arrange
        SpecBase<Guid,Guid> underlying = Spec
            .Build<Guid>(_ => false)
            .WhenTrue(underlyingTrueGuid)
            .WhenFalse(underlyingFalseGuid)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .WhenTrue(model => (model, isFalseMetadata))
            .WhenFalseYield((model, result) => result.Values.Select(meta => (model, meta)))
            .Create("is true");

        var result = spec.IsSatisfiedBy(guidModel);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([(guidModel, underlyingFalseGuid)]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_evaluate_minimally_defined_policies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(model);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_evaluate_minimally_defined_spec(bool model)
    {
        // Arrange
        SpecBase<bool,string> underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(model);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "¬is true")]
    public void Should_provide_a_reason_for_minimally_defined_policies(bool model, string expectedReason)
    {
        // Arrange
        PolicyBase<bool,string> underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "¬is true")]
    public void Should_provide_a_reason_for_minimally_defined_spec(bool model, string expectedReason)
    {
        // Arrange
        SpecBase<bool,string> underlying = Spec
            .Build((bool m) => m)
            .Create("is underlying true");

        var spec = Spec
            .Build(underlying)
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Fact]
    public void Should_generate_multi_metadata_description()
    {
        // Arrange
        var left = Spec
            .Build<string>(_ => true)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create("left");

        var right = Spec
            .Build<string>(_ => true)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .Create("right");

        var underlying = left | right;

        var spec = Spec
            .Build(underlying)
            .Create("top-level proposition");

        var result = spec.IsSatisfiedBy("model");

        // Act
        var act = result.Description.Reason;

        // Assert
        act.Should().NotContainAny("left true", "left false", "right true", "right false");

    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_policy_result(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Underlying;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_boolean_result(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Underlying;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_policy_result(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.UnderlyingWithValues;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_underlying_with_metadata_boolean_result(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model);

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.UnderlyingWithValues;

        // Assert
        act.Should().BeEquivalentTo([expected]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_have_a_description_that_has_a_causal_count_value_of_1_for_policies(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Description.CausalOperandCount;

        // Assert
        act.Should().Be(1);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_have_a_description_that_has_a_causal_count_value_of_1_for_specs(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Description.CausalOperandCount;

        // Assert
        act.Should().Be(1);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_policy_result(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).Causes;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Causes;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_boolean_result(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).Causes;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Causes;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_policy_result(bool model)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).CausesWithValues;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.CausesWithValues;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_create_a_boolean_result_that_contains_the_causal_with_metadata_boolean_result(bool model)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .Create("left");

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .Create("right");

        var orSpec = left | right;

        var expected = orSpec.IsSatisfiedBy(model).CausesWithValues;

        var spec = Spec
            .Build(orSpec)
            .Create("composite");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.CausesWithValues;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_policy_results(bool model, string expectedLeft, string expectedRight)
    {
        // Arrange
        PolicyBase<bool,string> left = Spec
            .Build((bool m) => m)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create();

        PolicyBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .Create();

        var underlying = left ^ !right;

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .WhenFalseYield((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .Create("top-level proposition");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedLeft, expectedRight);
    }

    [Theory]
    [InlineAutoData(true, "True: left true", "True: right false")]
    [InlineAutoData(false, "False: left false", "False: right true")]
    public void Should_permit_metadata_generated_using_underlying_boolean_results(bool model, string expectedLeft, string expectedRight)
    {
        // Arrange
        SpecBase<bool,string> left = Spec
            .Build((bool m) => m)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create();

        SpecBase<bool,string> right = Spec
            .Build((bool m) => !m)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .Create();

        var underlying = left ^ !right;

        var spec = Spec
            .Build(underlying)
            .WhenTrueYield((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .WhenFalseYield((satisfied, result) => result.Assertions.Select(assertion => $"{satisfied}: {assertion}"))
            .Create("top-level proposition");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedLeft, expectedRight);
    }

    [Theory]
    [InlineData(true, "propositional statement")]
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        PolicyBase<bool,string> underlying =
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
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        SpecBase<bool,string> underlying =
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
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        PolicyBase<bool,string> underlying =
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
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        SpecBase<bool,string> underlying =
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
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        PolicyBase<bool,string> underlying =
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
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        SpecBase<bool,string> underlying =
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
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_policies(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        PolicyBase<bool,string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalseYield((_, _) => [Metadata.False])
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
    [InlineData(false, "¬propositional statement")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_specs(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4));

        SpecBase<bool,string> underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(Metadata.False)
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse(_ => Metadata.False)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalse((_, _) => Metadata.False)
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(underlying)
                .WhenTrueYield((_, _) => Metadata.True.ToEnumerable())
                .WhenFalseYield((_, _) => [Metadata.False])
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
}
