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
        IEnumerable<IEnumerable<int>> expected  = [new[] { number }];
        
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
}