namespace Motiv.Tests;

public class PolicyTests
{
    [Theory, AutoData]
    public void Should_allow_access_to_underlying_policy_assertion_value(
        string trueResult,
        string falseResult)
    {
        // Arrange
        var underlyingPolicy =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(trueResult)
                .WhenFalse(falseResult)
                .Create("is underlying even policy");

        var policy =
            Spec.Build(underlyingPolicy)
                .WhenTrue((_, result) => result.Value)
                .WhenFalse((_, result) => result.Value)
                .Create("is even policy");

        // Act
        var result = policy.IsSatisfiedBy(2);

        // Assert
        result.Value.Should().Be(trueResult);
    }

    [Theory, AutoData]
    public void Should_allow_access_to_underlying_policy_assertion_value_for_multi_assertions(
        string trueResult,
        string falseResult)
    {
        // Arrange
        var underlyingPolicy =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(trueResult)
                .WhenFalse(falseResult)
                .Create("is underlying even policy");

        var spec =
            Spec.Build(underlyingPolicy)
                .WhenTrueYield((_, result) => result.Value.ToEnumerable())
                .WhenFalse((_, result) => result.Value)
                .Create("is even policy");

        // Act
        var result = spec.IsSatisfiedBy(2);

        // Assert
        result.Values.Should().BeEquivalentTo(trueResult);
    }

    [Theory, AutoData]
    public void Should_allow_access_to_underlying_policy_metadata_value(
        Guid trueResult,
        Guid falseResult)
    {
        // Arrange
        var underlyingPolicy =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(trueResult)
                .WhenFalse(falseResult)
                .Create("is underlying even policy");

        var policy =
            Spec.Build(underlyingPolicy)
                .WhenTrue((_, result) => result.Value)
                .WhenFalse((_, result) => result.Value)
                .Create("is even policy");

        // Act
        var result = policy.IsSatisfiedBy(2);

        // Assert
        result.Value.Should().Be(trueResult);
    }

    [Theory, AutoData]
    public void Should_allow_access_to_underlying_policy_metadata_value_for_multi_metadata(
        Guid trueResult,
        Guid falseResult)
    {
        // Arrange
        var underlyingPolicy =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(trueResult)
                .WhenFalse(falseResult)
                .Create("is underlying even policy");

        var policy =
            Spec.Build(underlyingPolicy)
                .WhenTrueYield((_, result) => result.Value.ToEnumerable())
                .WhenFalse((_, result) => result.Value)
                .Create("is even policy");

        // Act
        var result = policy.IsSatisfiedBy(2);

        // Assert
        result.Values.Should().BeEquivalentTo([trueResult]);
    }

    [Theory, AutoData]
    public void Should_allow_access_to_underlying_policy_result_string_value(
        string trueResult,
        string falseResult)
    {
        // Arrange
        var underlyingPolicy =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(trueResult)
                .WhenFalse(falseResult)
                .Create("is underlying even policy");

        var policy =
            Spec.Build((int m) => underlyingPolicy.IsSatisfiedBy(m))
                .WhenTrue((_, result) => result.Value)
                .WhenFalse((_, result) => result.Value)
                .Create("is even policy");

        // Act
        var result = policy.IsSatisfiedBy(2);

        // Assert
        result.Value.Should().Be(trueResult);
    }

    [Theory, AutoData]
    public void Should_allow_access_to_underlying_policy_result_metadata_value(
        Guid trueResult,
        Guid falseResult)
    {
        // Arrange
        var underlyingPolicy =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(trueResult)
                .WhenFalse(falseResult)
                .Create("is underlying even policy");

        var policy =
            Spec.Build((int m) => underlyingPolicy.IsSatisfiedBy(m))
                .WhenTrue((_, result) => result.Value)
                .WhenFalse((_, result) => result.Value)
                .Create("is even policy");

        // Act
        var result = policy.IsSatisfiedBy(2);

        // Assert
        result.Value.Should().Be(trueResult);
    }
}
