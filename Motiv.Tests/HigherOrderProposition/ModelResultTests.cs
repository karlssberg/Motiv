using Motiv.HigherOrderProposition;

namespace Motiv.Tests.HigherOrderProposition;

public class ModelResultTests
{
    [Theory]
    [AutoData]
    public void Constructor_ShouldInitializeProperties(string model, bool satisfied)
    {
        // Arrange & Act
        var result = new ModelResult<string>(model, satisfied);

        // Assert
        result.Model.Should().Be(model);
        result.Satisfied.Should().Be(satisfied);
    }

    [Theory]
    [AutoData]
    public void Constructor_ShouldInitializePropertiesWithComplexType(
        TestModel model,
        bool satisfied)
    {
        // Arrange & Act
        var result = new ModelResult<TestModel>(model, satisfied);

        // Assert
        result.Model.Should().Be(model);
        result.Satisfied.Should().Be(satisfied);
    }

    [Theory]
    [AutoData]
    public void Equality_ShouldBeTrue_WhenAllPropertiesAreEqual(
        TestModel model,
        bool satisfied)
    {
        // Arrange
        var result1 = new ModelResult<TestModel>(model, satisfied);
        var result2 = new ModelResult<TestModel>(model, satisfied);

        // Act & Assert
        result1.Should().Be(result2);
        (result1 == result2).Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void Equality_ShouldBeFalse_WhenModelIsDifferent(
        TestModel model1,
        TestModel model2,
        bool satisfied)
    {
        // Arrange
        var result1 = new ModelResult<TestModel>(model1, satisfied);
        var result2 = new ModelResult<TestModel>(model2, satisfied);

        // Act & Assert
        result1.Should().NotBe(result2);
        (result1 == result2).Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void Equality_ShouldBeFalse_WhenSatisfiedIsDifferent(
        TestModel model)
    {
        // Arrange
        var result1 = new ModelResult<TestModel>(model, true);
        var result2 = new ModelResult<TestModel>(model, false);

        // Act & Assert
        result1.Should().NotBe(result2);
        (result1 == result2).Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void GetHashCode_ShouldBeEqual_WhenPropertiesAreEqual(
        TestModel model,
        bool satisfied)
    {
        // Arrange
        var result1 = new ModelResult<TestModel>(model, satisfied);
        var result2 = new ModelResult<TestModel>(model, satisfied);

        // Act
        var hashCode1 = result1.GetHashCode();
        var hashCode2 = result2.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Theory]
    [AutoData]
    public void ToString_ShouldContainPropertyValues(
        TestModel model,
        bool satisfied)
    {
        // Arrange
        var result = new ModelResult<TestModel>(model, satisfied);

        // Act
        var toString = result.ToString();

        // Assert
        toString.Should().Contain(model.ToString());
        toString.Should().Contain(satisfied.ToString());
    }
}
