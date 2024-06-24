namespace Motiv.Tests;

public class EnumerableExtensionsTests
{
    [Theory]
    [AutoData]
    public void Should_convert_a_scalar_value_to_an_enumerable(int item)
    {
        // Act
        var act = item.ToEnumerable();

        // Assert
        act.Should().BeEquivalentTo([item]);
    }

    [Theory]
    [AutoData]
    public void Should_wrap_an_existing_enumerable_inside_an_enumerable(int number)
    {
        // Arrange
        IEnumerable<IEnumerable<int>> expected = [new[] { number }];

        IEnumerable<int> item = [number];

        // Act
        var act = item.ToEnumerable();

        // Assert
        act.Should()
            .BeEquivalentTo(expected).And
            .BeAssignableTo<IEnumerable<IEnumerable<int>>>();
    }

    [Fact]
    public void Should_be_able_to_easily_use_specs_with_linq_where_methods()
    {
        // Arrange
        IEnumerable<int> numbers = [-2, -1, 0, 1, 2];

        var spec =
            Spec.Build((int n) => n > 0)
                .WhenTrue("is positive")
                .WhenFalse("is not positive")
                .Create();

        // Act
        var act = numbers.Where(spec);

        // Assert
        act.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void Should_group_by_adjacent_even_numbers()
    {
        // Arrange
        IEnumerable<int> numbers = [11, 22, 44, 66, 77, 88, 99, 111, 122, 178, 201];
        IEnumerable<IEnumerable<int>> expected = [[11], [22, 44, 66], [77], [88], [99], [111], [122, 178], [201]];

        // Act
        var act = numbers.GroupAdjacentBy((left, right) =>
            left % 2 == 0 && right % 2 == 0);

        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Should_handle_empty_collection_when_grouping_by_adjacent_even_numbers()
    {
        // Arrange
        IEnumerable<int> numbers = [];
        IEnumerable<IEnumerable<int>> expected = [];

        // Act
        var act = numbers.GroupAdjacentBy((left, right) =>
            left % 2 == 0 && right % 2 == 0);

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(2, 3, false)]
    [InlineData(3, 2, true)]
    [InlineData(4, 4, true)]
    [InlineData(5, 7, false)]
    public void Should_identify_if_collection_has_at_least_n_items(int size, int n, bool expected)
    {
        // Arrange
        IEnumerable<int> numbers = Enumerable.Range(1, size);

        // Act
        var act = numbers.HasAtLeast(n);

        // Assert
        act.Should().Be(expected);
    }
}