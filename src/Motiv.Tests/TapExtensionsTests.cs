namespace Motiv.Tests;

public class TapExtensionsTests
{
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Tap_should_fire_callback_on_every_evaluation(
        bool satisfied,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => satisfied)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var callbackFired = false;
        var tapped = spec.Tap((m, r) => callbackFired = true);

        // Act
        tapped.IsSatisfiedBy(model);

        // Assert
        callbackFired.ShouldBeTrue();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Tap_should_receive_model_and_result(
        bool satisfied,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => satisfied)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        object? receivedModel = null;
        BooleanResultBase<string>? receivedResult = null;
        var tapped = spec.Tap((m, r) =>
        {
            receivedModel = m;
            receivedResult = r;
        });

        // Act
        tapped.IsSatisfiedBy(model);

        // Assert
        receivedModel.ShouldBeSameAs(model);
        receivedResult.ShouldNotBeNull();
        receivedResult!.Satisfied.ShouldBe(satisfied);
    }

    [Fact]
    public void TapWhenTrue_should_fire_callback_when_satisfied()
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => true)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var callbackFired = false;
        var tapped = spec.TapWhenTrue((m, r) => callbackFired = true);

        // Act
        tapped.IsSatisfiedBy(new object());

        // Assert
        callbackFired.ShouldBeTrue();
    }

    [Fact]
    public void TapWhenTrue_should_not_fire_callback_when_unsatisfied()
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => false)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var callbackFired = false;
        var tapped = spec.TapWhenTrue((m, r) => callbackFired = true);

        // Act
        tapped.IsSatisfiedBy(new object());

        // Assert
        callbackFired.ShouldBeFalse();
    }

    [Fact]
    public void TapWhenFalse_should_fire_callback_when_unsatisfied()
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => false)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var callbackFired = false;
        var tapped = spec.TapWhenFalse((m, r) => callbackFired = true);

        // Act
        tapped.IsSatisfiedBy(new object());

        // Assert
        callbackFired.ShouldBeTrue();
    }

    [Fact]
    public void TapWhenFalse_should_not_fire_callback_when_satisfied()
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => true)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var callbackFired = false;
        var tapped = spec.TapWhenFalse((m, r) => callbackFired = true);

        // Act
        tapped.IsSatisfiedBy(new object());

        // Assert
        callbackFired.ShouldBeFalse();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Tap_should_return_the_same_result_as_the_inner_spec(
        bool satisfied,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => satisfied)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var tapped = spec.Tap((_, _) => { });

        // Act
        var expectedResult = spec.IsSatisfiedBy(model);
        var actualResult = tapped.IsSatisfiedBy(model);

        // Assert
        actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
        actualResult.Assertions.ShouldBe(expectedResult.Assertions);
        actualResult.Reason.ShouldBe(expectedResult.Reason);
    }

    [Fact]
    public void Tap_should_preserve_description()
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => true)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        // Act
        var tapped = spec.Tap((_, _) => { });

        // Assert
        tapped.Description.Statement.ShouldBe(spec.Description.Statement);
        tapped.Description.Detailed.ShouldBe(spec.Description.Detailed);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Tap_should_delegate_matches_to_inner_spec(
        bool satisfied,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => satisfied)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var tapped = spec.Tap((_, _) => { });

        // Act
        var result = tapped.Matches(model);

        // Assert
        result.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_chain_multiple_taps(
        bool satisfied,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => satisfied)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var whenTrueFired = false;
        var whenFalseFired = false;

        var tapped = spec
            .TapWhenTrue((_, _) => whenTrueFired = true)
            .TapWhenFalse((_, _) => whenFalseFired = true);

        // Act
        tapped.IsSatisfiedBy(model);

        // Assert
        whenTrueFired.ShouldBe(satisfied);
        whenFalseFired.ShouldBe(!satisfied);
    }

    [Fact]
    public void Tap_should_return_underlying_spec()
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => true)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var tapped = spec.Tap((_, _) => { });

        // Act
        var underlying = tapped.Underlying;

        // Assert
        underlying.ShouldBe(spec.ToEnumerable());
    }
}
