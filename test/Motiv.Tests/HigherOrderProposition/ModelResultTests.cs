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
        result.Model.ShouldBe(model);
        result.Satisfied.ShouldBe(satisfied);
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
        result.Model.ShouldBe(model);
        result.Satisfied.ShouldBe(satisfied);
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
        result1.ShouldBe(result2);
        (result1 == result2).ShouldBeTrue();
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
        result1.ShouldNotBe(result2);
        (result1 == result2).ShouldBeFalse();
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
        result1.ShouldNotBe(result2);
        (result1 == result2).ShouldBeFalse();
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
        hashCode1.ShouldBe(hashCode2);
    }
}
