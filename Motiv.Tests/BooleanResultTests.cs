namespace Motiv.Tests;

public class BooleanResultTests
{
    [Fact]
    public void Should_ensure_that_the_true_operator_overloads_are_correct()
    {
        // Arrange
        var spec = Spec
            .Build((object _) => true)
            .Create("is model true");

        var result = spec.IsSatisfiedBy(new object());
        // Act
        var act = result && result; // invokes true operator overload
        
        // Assert
        act.Satisfied.Should().BeTrue();
    }
    
    [Fact]
    public void Should_ensure_that_the_false_operator_overloads_are_correct()
    {
        // Arrange
        var spec = Spec
            .Build((object _) => false)
            .Create("is model true");

        var result = spec.IsSatisfiedBy(new object());
        
        // Act
        var act = !result && result; // invokes false operator overload

        // Assert
        act.Satisfied.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_perform_and_also_on_boolean_results(bool leftModel, bool rightModel, bool expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        // Act
        var act = left.AndAlso(right);

        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, "left is false")]
    [InlineData(false, true, "left is false")]
    [InlineData(true, false, "right is false")]
    [InlineData(true, true, "left is true", "right is true")]
    public void Should_assert_and_also_on_boolean_results_using_member_method(bool leftModel, bool rightModel, params string[] expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        var result = left.AndAlso(right);
        
        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    [Theory]
    [InlineData(false, false, "left is false")]
    [InlineData(false, true, "left is false")]
    [InlineData(true, false, "right is false")]
    [InlineData(true, true, "left is true", "right is true")]
    public void Should_assert_and_also_on_boolean_results_using_operator(bool leftModel, bool rightModel, params string[] expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        var result = left && right;
        
        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_perform_and_also_on_boolean_results_with_different_metadata_types(
        bool leftModel,
        bool rightModel,
        bool expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        // Act
        var act = left.AndAlso(right);

        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Should_perform_or_else_on_boolean_results(
        bool leftModel,
        bool rightModel,
        bool expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        // Act
        var act = left.OrElse(right);

        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, "left is false", "right is false")]
    [InlineData(false, true, "right is true")]
    [InlineData(true, false, "left is true")]
    [InlineData(true, true, "left is true")]
    public void Should_assert_or_else_on_boolean_results_using_member_method(bool leftModel, bool rightModel, params string[] expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        var result = left.OrElse(right);
        
        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(false, false, "left is false", "right is false")]
    [InlineData(false, true, "right is true")]
    [InlineData(true, false, "left is true")]
    [InlineData(true, true, "left is true")]
    public void Should_assert_or_else_on_boolean_results_using_operator(bool leftModel, bool rightModel, params string[] expected)
    {
        // Arrange
        var left = Spec
            .Build((bool b) => b)
            .WhenTrue("left is true")
            .WhenFalse("left is false")
            .Create()
            .IsSatisfiedBy(leftModel);
        
        var right = Spec
            .Build((bool b) => b)
            .WhenTrue("right is true")
            .WhenFalse("right is false")
            .Create()
            .IsSatisfiedBy(rightModel);

        var result = left || right;
        
        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Reason_property_during_and_operation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => true).Create("a");
        var specB = Spec.Build((bool _) => false).Create("b");
        var specC = Spec.Build((bool _) => false).Create("c");

        // Compose new proposition
        var spec = specA & !!!(specB | specC);

        // Evaluate proposition
        var sut = spec.IsSatisfiedBy(true);

        var act = sut.Reason;
        
        act.Should().Be("a & (!b | !c)");
    }
    
    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Reason_property_during_andAlso_operation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => true).Create("a");
        var specB = Spec.Build((bool _) => false).Create("b");
        var specC = Spec.Build((bool _) => false).Create("c");

        // Compose new proposition
        var spec = specA.AndAlso(!!!(specB | specC));

        // Evaluate proposition
        var sut = spec.IsSatisfiedBy(true);

        var act = sut.Reason;
        
        act.Should().Be("a && (!b | !c)");
    }
    
    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Reason_property_during_or_operation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => true).Create("a");
        var specB = Spec.Build((bool _) => false).Create("b");
        var specC = Spec.Build((bool _) => false).Create("c");

        // Compose new proposition
        var spec = specA | !!!(specB & specC);

        // Evaluate proposition
        var sut = spec.IsSatisfiedBy(true);

        var act = sut.Reason;
        
        act.Should().Be("a | (!b & !c)");
    }
    
    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Reason_property_during_orElse_operation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => false).Create("a");
        var specB = Spec.Build((bool _) => true).Create("b");
        var specC = Spec.Build((bool _) => true).Create("c");

        // Compose new proposition
        var spec = specA.OrElse(!!!(specB & specC));

        // Evaluate proposition
        var sut = spec.IsSatisfiedBy(true);

        var act = sut.Reason;
        
        act.Should().Be("!a || (b & c)");
    }

}