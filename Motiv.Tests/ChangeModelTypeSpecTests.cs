namespace Motiv.Tests;

public class ChangeModelTypeSpecTests
{
    [Theory]
    [InlineAutoData("hello", false)]
    [InlineAutoData(default(string), true)]
    public void Should_change_the_model(string? model, bool expected)
    {
        // Arrange
        var isEmpty = Spec
            .Build((object? m) => m is null)
            .WhenTrue("is null")
            .WhenFalse("is not null")
            .Create();

        var spec = isEmpty.ChangeModelTo<string?>();

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData("hello", false)]
    [InlineAutoData(1, true)]
    [InlineAutoData(1d, true)]
    [InlineAutoData(false, true)]
    [InlineAutoData(typeof(object), false)]
    public void Should_change_the_model_using_a_model_selector_function(object model, bool expected)
    {
        // Arrange
        var isValueType = Spec
            .Build((Type t) => t.IsValueType)
            .WhenTrue("is value-type")
            .WhenFalse("is not value-type")
            .Create();

        var spec = isValueType.ChangeModelTo<object>(m => m.GetType());

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData("hello", true)]
    [InlineAutoData("world", true)]
    [InlineAutoData("h1e234j", false)]
    public void Should_logically_resolve_parent_specification(string model, bool expected)
    {
        // Arrange
        var isLetter = 
            Spec.Build<char>(char.IsLetter)
                .WhenTrue(ch => $"'{ch}' is a letter")
                .WhenFalse(ch => $"'{ch}' is not a letter")
                .Create("is a letter");

        var isAllLetters = 
            Spec.Build(isLetter)
                .As(result => result.AllTrue())
                .WhenTrue("all characters are letters")
                .WhenFalse(evaluation => evaluation.Assertions)
                .Create() 
                .ChangeModelTo<string>(m => m.ToCharArray());

        var result = isAllLetters.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData("#hello", "'#' is not a letter")]
    [InlineAutoData("!world!", "'!' is not a letter")]
    [InlineAutoData("ok", "'o' is a letter", "'k' is a letter")]
    public void Should_identify_non_letters(string model, params string[] expected)
    {
        // Arrange
        var isLetter = Spec
            .Build<char>(char.IsLetter)
            .WhenTrue(ch => $"'{ch}' is a letter")
            .WhenFalse(ch => $"'{ch}' is not a letter")
            .Create("is a letter");

        var isAllLetters = Spec
            .Build(isLetter)
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Assertions)
            .WhenFalse(evaluation => evaluation.Assertions)
            .Create("has all letters")
            .ChangeModelTo<string>(m => m.ToCharArray());

        var result = isAllLetters.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Should_delegate_toString_method_to_underlying_spec()
    {
        // Arrange
        var isLetter = Spec
            .Build<char>(char.IsLetter)
            .WhenTrue(ch => $"'{ch}' is a letter")
            .WhenFalse(ch => $"'{ch}' is not a letter")
            .Create("is a letter");

        var isAllLettersAsCharArray = Spec
            .Build(isLetter)
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Assertions)
            .WhenFalse(evaluation => evaluation.Assertions)
            .Create("has all letters");
            
        var isAllLetters = isAllLettersAsCharArray
            .ChangeModelTo<string>(m => m.ToCharArray());

        // Act
        var act = isAllLetters.ToString();

        // Assert
        act.Should().Be(isAllLetters.ToString());
    }
}