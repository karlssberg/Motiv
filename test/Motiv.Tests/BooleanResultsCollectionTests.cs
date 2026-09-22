namespace Motiv.Tests;

public class BooleanResultsCollectionTests
{
    private static SpecBase<int, string> IsEven =>
        Spec.Build((int n) => n % 2 == 0)
            .WhenTrue("even")
            .WhenFalse("odd")
            .Create("is even");

    [Fact]
    public void Should_yield_only_satisfied_models_on_each_enumeration()
    {
        // Arrange
        var numbers = Enumerable.Range(1, 6);

        // Act
        var result = numbers.Where(IsEven);

        // Assert
        result.AsEnumerable().ShouldBe((IEnumerable<int>)[2, 4, 6]);
        result.AsEnumerable().ShouldBe((IEnumerable<int>)[2, 4, 6]);
    }

    [Fact]
    public void Should_expose_all_evaluated_models()
    {
        // Arrange
        var numbers = Enumerable.Range(1, 4);

        // Act
        var result = numbers.Where(IsEven);

        // Assert
        result.Models.ShouldBe([1, 2, 3, 4]);
        result.Models.ShouldBeSameAs(result.Models);
    }

    [Fact]
    public void Should_expose_metadata_values_for_all_results()
    {
        // Arrange
        var numbers = Enumerable.Range(1, 4);

        // Act
        var result = numbers.Where(IsEven);

        // Assert
        result.Values.ShouldBe(["odd", "even", "odd", "even"]);
        result.Values.ShouldBeSameAs(result.Values);
    }

    [Fact]
    public void Should_expose_distinct_assertions_across_all_results()
    {
        // Arrange
        var numbers = Enumerable.Range(1, 4);

        // Act
        var result = numbers.Where(IsEven);

        // Assert
        result.Assertions.ShouldBe(["is even == false", "is even == true"]);
        result.Assertions.ShouldBeSameAs(result.Assertions);
    }

    [Fact]
    public void Should_expose_a_result_per_model()
    {
        // Arrange
        var numbers = Enumerable.Range(1, 4);

        // Act
        var result = numbers.Where(IsEven);

        // Assert
        result.Results.Select(r => r.Satisfied).ShouldBe([false, true, false, true]);
        result.Results.ShouldBeSameAs(result.Results);
    }

    [Fact]
    public void Should_derive_projections_from_the_materialized_results_when_results_are_read_first()
    {
        // Arrange
        var numbers = Enumerable.Range(1, 4);
        var result = numbers.Where(IsEven);

        // Act
        var results = result.Results;

        // Assert
        result.Models.ShouldBe([1, 2, 3, 4]);
        result.Values.ShouldBe(["odd", "even", "odd", "even"]);
        result.Assertions.ShouldBe(["is even == false", "is even == true"]);
        result.AsEnumerable().ShouldBe((IEnumerable<int>)[2, 4]);
        results.Count().ShouldBe(4);
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_results()
    {
        // Act
        var act = () => new BooleanResultsCollection<int, string>(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>()
            .ParamName!.ShouldBe("results");
    }
}
