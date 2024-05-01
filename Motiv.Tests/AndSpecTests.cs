using System.Text.RegularExpressions;
using FluentAssertions;

namespace Motiv.Tests;

public class AndSpecTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, false, false)]
    public void Should_perform_logical_and(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var sut = left & right;

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(expected);
        result.Metadata.Should().AllBeEquivalentTo(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "left & right")]
    [InlineAutoData(true, false, "!right")]
    [InlineAutoData(false, true, "!left")]
    [InlineAutoData(false, false, "!left & !right")]
    public void Should_serialize_the_result_of_the_and_operation(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var sut = left & right;

        var result = sut.IsSatisfiedBy(model);

        result.Reason.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "True & True")]
    [InlineAutoData(true, false, "False")]
    [InlineAutoData(false, true, "False")]
    [InlineAutoData(false, false, "False & False")]
    public void
        Should_serialize_the_result_of_the_and_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
            bool leftResult,
            bool rightResult,
            string expected,
            object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var spec = left & right;

        // Act
        var act = spec.IsSatisfiedBy(model).Reason;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_statement_of_the_specification_when_using_metadata_specification(bool leftResult, bool rightResult)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var expected = $"{left.Description} & {right.Description}";

        // Act
        var act = (left & right).Statement;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_statement_of_the_specification_when_using_explanation_specification(bool leftResult,
        bool rightResult)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec.Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var expected = $"{left.Description} & {right.Description}";

        // Act
        var act = (left & right).Statement;

        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_correctly_serialize_the_specification_when_using_explanation_specification(bool leftResult,
        bool rightResult)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec.Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var expected = $"{left.Description} & {right.Description}";

        // Act
        var act = (left & right).Statement;

        // Assert
        act.Should().Be(expected);
    }

    private record Subscription(DateTime Start, DateTime End)
    {
        public DateTime Start { get; } = Start;
        public DateTime End { get; } = End;
    }

    private class IsSubscriptionActive(DateTime now) : Spec<Subscription>(() =>
    {
        var hasSubscriptionStarted = Spec
            .Build((Subscription s) => s.Start < now)
            .WhenTrue("subscription has started")
            .WhenFalse("subscription has not started")
            .Create();

        var hasSubscriptionEnded = Spec
            .Build((Subscription s) => s.End < now)
            .WhenTrue("subscription has ended")
            .WhenFalse("subscription has not ended")
            .Create();

        return Spec
            .Build(hasSubscriptionStarted & !hasSubscriptionEnded)
            .WhenTrue("subscription is active")
            .WhenFalse("subscription is inactive")
            .Create();
    });


    [Fact]
    public void Should_evaluate_underlying_explanations_with_a_complex_model()
    {
        // Arrange
        var now = DateTime.Now;
        var isActive = new IsSubscriptionActive(now);

        var subscription = new Subscription(now.Date, now.AddDays(1));

        // Act
        var result = isActive.IsSatisfiedBy(subscription).Explanation.Underlying;
        
        // Assert
        result.GetAssertions().Should().BeEquivalentTo(
        [
            "subscription has started",
            "subscription has not ended"
        ]);
    }
    
    [Fact]
    public void Should_evaluate_assertions_with_a_complex_model()
    {
        // Arrange
        var now = DateTime.Now;
        var isActive = new IsSubscriptionActive(now);

        var subscription = new Subscription(now.Date, now.AddDays(1));

        // Act
        var result = isActive.IsSatisfiedBy(subscription).Assertions;

        // Assert
        result.Should().BeEquivalentTo("subscription is active");
    }

    private enum Country
    {
        Usa
    }

    private record Device(Country Country)
    {
        public Country Country { get; } = Country;
    }


    [Theory]
    [AutoData]
    public void Should_format_the_reason_from_the_results_obtained_from_two_specs_of_different_models(DateTime now)
    {
        // Arrange
        var hasSubscriptionStartedSpec = Spec
            .Build<Subscription>(s => s.Start < now)
            .WhenTrue("subscription has started")
            .WhenFalse("subscription has not started")
            .Create();

        var hasSubscriptionEndedSpec = Spec
            .Build<Subscription>(s => s.End < now)
            .WhenTrue("subscription has ended")
            .WhenFalse("subscription has not ended")
            .Create();

        var isLocationUsaSpec = Spec
            .Build<Device>(device => device.Country == Country.Usa)
            .WhenTrue("the location is in the USA")
            .WhenFalse("the location is outside the USA")
            .Create();

        var isActiveSpec = hasSubscriptionStartedSpec & !hasSubscriptionEndedSpec;
        var isActive = isActiveSpec.IsSatisfiedBy(new Subscription(now.Date, now.AddDays(1)));
        var isUsa = isLocationUsaSpec.IsSatisfiedBy(new Device(Country.Usa));

        // Act
        var result = (isActive & isUsa).Reason;

        // Assert
        result.Should().Be("subscription has started & subscription has not ended & the location is in the USA");
    }
    
    [Theory]
    [AutoData]
    public void Should_obtain_assertions_from_the_results_obtained_from_two_specs_of_different_models(DateTime now)
    {
        // Arrange
        var hasSubscriptionStartedSpec = Spec
            .Build<Subscription>(s => s.Start < now)
            .WhenTrue("subscription has started")
            .WhenFalse("subscription has not started")
            .Create();

        var hasSubscriptionEndedSpec = Spec
            .Build<Subscription>(s => s.End < now)
            .WhenTrue("subscription has ended")
            .WhenFalse("subscription has not ended")
            .Create();

        var isLocationUsaSpec = Spec
            .Build<Device>(device => device.Country == Country.Usa)
            .WhenTrue("the location is in the USA")
            .WhenFalse("the location is outside the USA")
            .Create();

        var isActiveSpec = hasSubscriptionStartedSpec & !hasSubscriptionEndedSpec;
        var isActive = isActiveSpec.IsSatisfiedBy(new Subscription(now.Date, now.AddDays(1)));
        var isUsa = isLocationUsaSpec.IsSatisfiedBy(new Device(Country.Usa));

        // Act
        var act = (isActive & isUsa).Assertions;

        // Assert
        act.Should().BeEquivalentTo(
        [
            "subscription has started",
            "subscription has not ended",
            "the location is in the USA"
        ]);
    }

    [Theory]
    [InlineAutoData(false, false, 2)]
    [InlineAutoData(false, true, 1)]
    [InlineAutoData(true, false, 1)]
    [InlineAutoData(true, true, 2)]
    public void Should_accurately_report_the_number_of_causal_operands(bool left, bool right, int expected,
        object model)
    {
        // Arrange
        var leftSpec = Spec
            .Build<object>(_ => left)
            .Create("left");

        var rightSpec = Spec
            .Build<object>(_ => right)
            .Create("right");

        var spec = leftSpec & rightSpec;

        // Act
        var act = spec.IsSatisfiedBy(model).Description.CausalOperandCount;

        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_perform_And_on_specs_with_different_metadata(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        Guid leftTrue,
        Guid leftFalse,
        int  rightTrue,
        int  rightFalse)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(leftTrue)
                .WhenFalse(leftFalse)
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(rightTrue)
                .WhenFalse(rightFalse)
                .Create("right");

        var spec = left & right;
        
        // Act
        var act = spec.IsSatisfiedBy("").Satisfied;

        // Assert
        act.Should().Be(expectedSatisfied);
    }
    
    [Theory]
    [InlineData(false, false, "!left", "!right")]
    [InlineData(false, true, "!left")]
    [InlineData(true, false, "!right")]
    [InlineData(true, true, "left", "right")]
    public void Should_perform_And_on_specs_with_different_metadata_and_preserve_assertions(
        bool leftValue,
        bool rightValue,
        params string[] expectedAssertions)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var spec = left & right;
        
        // Act
        var act = spec.IsSatisfiedBy("").Assertions;

        act.Should().BeEquivalentTo(expectedAssertions);
    }
    
    [Theory]
    [InlineData(false, false, "!left", "!right")]
    [InlineData(false, true, "!left")]
    [InlineData(true, false, "!right")]
    [InlineData(true, true, "left", "right")]
    public void Should_perform_And_on_specs_with_different_metadata_and_preserve_metadata(
        bool leftValue,
        bool rightValue,
        params string[] expectedAssertions)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var spec = left & right;
        
        // Act
        var act = spec.IsSatisfiedBy("").Metadata;

        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }
    
    [Fact]
    public void Should_not_collapse_ORELSE_operators_in_spec_description()
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => true)
            .Create("first");
        
        var second = Spec
            .Build<bool>(_ => true)
            .Create("second");
        
        var third = Spec
            .Build<bool>(_ => true)
            .Create("third");

        var spec = first & second & third;

        // Act
        var act = spec.Expression;
        
        // Assert
        act.Should().Be(
            """
            AND
                first
                second
                third
            """);
    }
    
    [Fact]
    public void Should_return_the_underlying_specs()
    {
        // Arrange
        var left = Spec
            .Build<bool>(_ => true)
            .Create("left");
        
        var right = Spec
            .Build<bool>(_ => true)
            .Create("right");
        
        // Act
        var act = (left & right).Underlying;
        
        // Assert
        act.Should().BeEquivalentTo([left, right]);
    }
}