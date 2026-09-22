namespace Motiv.Tests;

public class QueryableExtensionsTests
{
    [Fact]
    public void Should_filter_a_queryable_using_an_expression_spec()
    {
        // Arrange
        var isAdult = Spec.From((int age) => age >= 18).Create("is adult");
        var queryable = new[] { 12, 18, 30, 65 }.AsQueryable();

        // Act
        var act = queryable.Where(isAdult).ToArray();

        // Assert
        act.ShouldBe([18, 30, 65]);
    }

    [Fact]
    public void Should_filter_a_queryable_using_a_composed_expression_spec()
    {
        // Arrange
        var isAdult = Spec.From((int age) => age >= 18).Create("is adult");
        var isSenior = Spec.From((int age) => age >= 65).Create("is senior");
        var workingAge = isAdult.And(!isSenior);
        var queryable = new[] { 12, 18, 30, 65 }.AsQueryable();

        // Act
        var act = queryable.Where(workingAge).ToArray();

        // Assert
        act.ShouldBe([18, 30]);
    }
}
