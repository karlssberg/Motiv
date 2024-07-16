namespace Motiv.Tests;

public class PolicyTests
{
    public class TestPolicy() : Policy<bool>(
        Spec.Build(UnderlyingSpec)
            .WhenTrue(True)
            .WhenFalse(False)
            .Create(PrimaryStatement))
    {
        public const string True = "true";
        public const string False = "false";
        public const string UnderlyingStatement = "underlying";
        public const string PrimaryStatement = "is true";

        public static PolicyBase<bool, string> UnderlyingSpec => Spec
            .Build((bool b) => b)
            .Create(UnderlyingStatement);
    }

    public class TestFromFactoryPolicy() : Policy<bool>(() =>
        Spec.Build(UnderlyingSpec)
            .WhenTrue(True)
            .WhenFalse(False)
            .Create(PrimaryStatement))
    {
        public const string True = "true";
        public const string False = "false";
        public const string UnderlyingStatement = "underlying";
        public const string PrimaryStatement = "is true";

        public static PolicyBase<bool, string> UnderlyingSpec => Spec
            .Build((bool b) => b)
            .Create(UnderlyingStatement);
    }

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
                .WhenTrueYield((_, result) => result.Values)
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

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Should_permit_a_policy_in_custom_policy_constructor(
        bool model,
        bool expected)
    {
        // Arrange
        var policy = new TestPolicy();

        // Act
        var result = policy.IsSatisfiedBy(model);

        // Assert
        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, TestPolicy.True)]
    [InlineData(false, TestPolicy.False)]
    public void Should_yield_value_for_a_policy_in_custom_policy_constructor(
        bool model,
        string expected)
    {
        // Arrange
        var policy = new TestPolicy();

        // Act
        var result = policy.IsSatisfiedBy(model);

        // Assert
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Should_permit_a_policy_factory_in_custom_policy_constructor(
        bool model,
        bool expected)
    {
        // Arrange
        var policy = new TestFromFactoryPolicy();

        // Act
        var result = policy.IsSatisfiedBy(model);

        // Assert
        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, TestFromFactoryPolicy.True)]
    [InlineData(false, TestFromFactoryPolicy.False)]
    public void Should_yield_value_for_a_policy_factory_in_custom_policy_constructor(
        bool model,
        string expected)
    {
        // Arrange
        var policy = new TestFromFactoryPolicy();

        // Act
        var result = policy.IsSatisfiedBy(model);

        // Assert
        result.Value.Should().Be(expected);
    }
}
