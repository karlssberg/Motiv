using AutoFixture.Xunit2;
using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class TutorialTests
{
    [Fact]
    public void Should_deomo_a_basic_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .Create("is even");

        isEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEven.IsSatisfiedBy(2).Reason.Should().BeEquivalentTo("is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("is even");

        isEven.IsSatisfiedBy(3).Satisfied.Should().BeFalse();
        isEven.IsSatisfiedBy(3).Reason.Should().BeEquivalentTo("!is even");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("!is even");
    }

    [Fact]
    public void Should_deomo_a_basic_spec_using_strings_as_assertions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("number is even")
            .WhenFalse("number is odd")
            .Create();

        isEven.IsSatisfiedBy(2).Reason.Should().Be("number is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("number is even");

        isEven.IsSatisfiedBy(3).Reason.Should().Be("number is odd");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("number is odd");
    }

    [Fact]
    public void Should_demo_a_basic_spec_using_functions_as_assertion_functions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n} is even")
            .WhenFalse(n => $"{n} is odd")
            .Create("is even");

        isEven.IsSatisfiedBy(2).Reason.Should().Be("is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("2 is even");

        isEven.IsSatisfiedBy(3).Reason.Should().Be("!is even");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("3 is odd");
    }

    [Fact]
    public void Should_demo_handling_multiple_languages_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => new { English = "the number is even", Spanish = "el número es par" })
            .WhenFalse(n => new { English = "the number is odd", Spanish = "el número es impar" })
            .Create("is even number");

        isEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEven.IsSatisfiedBy(2).Reason.Should().Be("is even number");
        isEven.IsSatisfiedBy(2).Metadata.Select(m => m.English).Should().BeEquivalentTo("the number is even");
        isEven.IsSatisfiedBy(2).Metadata.Select(m => m.Spanish).Should().BeEquivalentTo("el número es par");
    }

    [Fact]
    public void Should_demo_composite_spec()
    {
        var isPositive = Spec
            .Build<int>(n => n > 0)
            .WhenTrue("the number is positive")
            .WhenFalse(n => $"the number is {(n < 0 ? "negative" : "zero")}")
            .Create();

        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("the number is even")
            .WhenFalse("the number is odd")
            .Create();

        var isPositiveAndEven = isPositive & isEven;

        isPositiveAndEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isPositiveAndEven.IsSatisfiedBy(2).Reason.Should().Be("the number is positive & the number is even");
        isPositiveAndEven.IsSatisfiedBy(2).Assertions.Should()
            .BeEquivalentTo("the number is positive", "the number is even");

        isPositiveAndEven.IsSatisfiedBy(3).Satisfied.Should().BeFalse();
        isPositiveAndEven.IsSatisfiedBy(3).Reason.Should().Be("the number is odd");
        isPositiveAndEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("the number is odd");

        isPositiveAndEven.IsSatisfiedBy(-2).Satisfied.Should().BeFalse();
        isPositiveAndEven.IsSatisfiedBy(-2).Reason.Should().Be("the number is negative");
        isPositiveAndEven.IsSatisfiedBy(-2).Assertions.Should().BeEquivalentTo("the number is negative");
    }

    [Fact]
    public void Should_demonstrate_higher_order_factory_methods()
    {
        var isNegative =
            Spec.Build((int n) => n < 0)
                .WhenTrue("the number is negative")
                .WhenFalse("the number is not negative")
                .Create();

        var allAreNegativeSpec =
            Spec.Build(isNegative)
                .AsAllSatisfied()
                .WhenTrue("all are negative")
                .WhenFalse(evaluation => evaluation switch
                {
                    { FalseCount: <= 10 } => evaluation.FalseModels.Select(n => $"{n}  is not negative"),
                    _ => $"{evaluation.FalseCount} of {evaluation.Count} are not negative".ToEnumerable()
                })
                .Create();

        var act = allAreNegativeSpec.IsSatisfiedBy([-2, -1, 0, 1, 2]);

        act.Satisfied.Should().BeFalse();
        act.Assertions.Should().BeEquivalentTo("0  is not negative", "1  is not negative", "2  is not negative");
    }

    [Fact]
    public void Should_demonstrate_composition_using_spec_type()
    {
        var isEvenSpec = 
            Spec.Build((int n) => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();
        
        var isPositiveSpec =
            Spec.Build((int n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();
        
        var isEvenAndPositiveSpec =
            Spec.Build(isEvenSpec & isPositiveSpec)
                .WhenTrue("the number is even and positive")
                .WhenFalse((_,evaluation) => $"the number is {string.Join(" and ", evaluation.Assertions)}")
                .Create();
        
        isEvenAndPositiveSpec.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEvenAndPositiveSpec.IsSatisfiedBy(2).Reason.Should().Be("the number is even and positive");
        isEvenAndPositiveSpec.IsSatisfiedBy(-2).Reason.Should().Be("the number is not positive");
        isEvenAndPositiveSpec.IsSatisfiedBy(-3).Reason.Should().Be("the number is odd and not positive");
    }

    public record BasketItem(bool FreeShipping);
    public record Basket(ICollection<BasketItem> Items);
    
    [Fact]
    public void Should_demonstrate_and_also_operation()
    {
        var emptyBasket = new Basket(Array.Empty<BasketItem>());
        var isBasketEmptySpec =
            Spec.Build((Basket b) => b.Items.Count == 0)
                .WhenTrue("basket is empty")
                .WhenFalse(o => $"basket contains {o.Items.Count} items")
                .Create();

        var isFreeShippingSpec = 
            Spec.Build((Basket b) => b.Items.All(i => i.FreeShipping))
                .WhenTrue("free shipping")
                .WhenFalse("shipping payment required")
                .Create();

        var showShippingPageButton = (!isBasketEmptySpec).AndAlso(!isFreeShippingSpec);

        var result = showShippingPageButton.IsSatisfiedBy(emptyBasket);
        
        result.Satisfied.Should().BeFalse();
        result.Reason.Should().Be("basket is empty");
        result.Reason.Should().BeEquivalentTo("basket is empty");
    }

    [Fact]
    public void Should_demonstrate_is_even_spec_as_an_all_satisfied_higher_order_logic()
    {
        var isEven =
            Spec.Build<int>(n => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();
        
        var allAreEven =
            Spec.Build(isEven)
                .AsAllSatisfied()
                .WhenTrue(evaluation =>
                    evaluation switch 
                    { 
                        { NoneSatisfied: true } => "the collection is empty",
                        { TrueCount: 1 } => $"{evaluation.TrueModels.Serialize()} is even and is the only item",
                        _ => "all are even"
                    })
                .WhenFalse(evaluation =>
                    evaluation switch
                    {
                        { Count: 1 } => [$"{evaluation.FalseModels.Serialize()} is odd and is the only item"],
                        { FalseCount: 1 } => [$"only {evaluation.FalseModels.Serialize()} is odd"],
                        { NoneSatisfied: true } => ["all are odd"],
                        _ => evaluation.FalseModels.Select(n => $"{n} is odd")
                    })
                .Create("all are even");
        
        allAreEven.IsSatisfiedBy([2, 4, 6, 8]).Satisfied.Should().BeTrue();
        allAreEven.IsSatisfiedBy([2, 4, 6, 8]).Assertions.Should().BeEquivalentTo("all are even");
        
        allAreEven.IsSatisfiedBy([10]).Satisfied.Should().BeTrue();
        allAreEven.IsSatisfiedBy([10]).Assertions.Should().BeEquivalentTo("10 is even and is the only item");
        
        
        allAreEven.IsSatisfiedBy([11]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([11]).Assertions.Should().BeEquivalentTo("11 is odd and is the only item");
        
        allAreEven.IsSatisfiedBy([2, 4, 6, 9]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([2, 4, 6, 9]).Assertions.Should().BeEquivalentTo("only 9 is odd");
        
        allAreEven.IsSatisfiedBy([]).Satisfied.Should().BeTrue();
        allAreEven.IsSatisfiedBy([]).Assertions.Should().BeEquivalentTo("the collection is empty");
        
        allAreEven.IsSatisfiedBy([1, 3, 5, 7]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([1, 3, 5, 7]).Assertions.Should().BeEquivalentTo("all are odd");
        
        allAreEven.IsSatisfiedBy([2, 4, 5, 7]).Satisfied.Should().BeFalse();
        allAreEven.IsSatisfiedBy([2, 4, 5, 7]).Assertions.Should().BeEquivalentTo("5 is odd", "7 is odd");
    }
}