using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ChangeModelTypeSpecTests
{
    [Theory]
    [InlineAutoData("hello", false)]
    [InlineAutoData(default(string), true)]
    public void Should_change_the_model(string? model, bool expected)
    {
        var isEmpty = Spec
            .Build<object?>(m => m is null)
            .YieldWhenTrue("is null")
            .YieldWhenFalse("is not null")
            .CreateSpec();

        var sut = isEmpty.ChangeModel<string?>();

        var act = sut.IsSatisfiedBy(model);

        act.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData("hello", false)]
    [InlineAutoData(1, true)]
    [InlineAutoData(1d, true)]
    [InlineAutoData(false, true)]
    [InlineAutoData(typeof(object), false)]
    public void Should_change_the_model_using_a_model_selector_function(object model, bool expected)
    {
        var isValueType = Spec
            .Build<Type>(t => t.IsValueType)
            .YieldWhenTrue("is value-type")
            .YieldWhenFalse("is not value-type")
            .CreateSpec();

        var sut = isValueType.ChangeModel<object>(m => m.GetType());

        var act = sut.IsSatisfiedBy(model);

        act.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData("hello", true)]
    [InlineAutoData("world", true)]
    [InlineAutoData("h1e234j", false)]
    public void Should_logically_resolve_parent_specification(string model, bool expected)
    {
        var isLetter = Spec
            .Build<char>(char.IsLetter)
            .YieldWhenTrue(ch => $"'{ch}' is a letter")
            .YieldWhenFalse(ch => $"'{ch}' is not a letter")
            .CreateSpec("is a letter");

        var isAllLetters = isLetter
            .All("all characters are letters")
            .ChangeModel<string>(m => m.ToCharArray());

        var act = isAllLetters.IsSatisfiedBy(model);

        act.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData("#hello", "'#' is not a letter")]
    [InlineAutoData("!world!", "'!' is not a letter")]
    [InlineAutoData("ok", "'o' is a letter", "'k' is a letter")]
    public void Should_identify_non_letters(string model, params string[] expected)
    {
        var isLetter = Spec
            .Build<char>(char.IsLetter)
            .YieldWhenTrue(ch => $"'{ch}' is a letter")
            .YieldWhenFalse(ch => $"'{ch}' is not a letter")
            .CreateSpec("is a letter");

        var isAllLetters = isLetter
            .All()
            .ChangeModel<string>(m => m.ToCharArray());

        var act = isAllLetters.IsSatisfiedBy(model);

        act.GetMetadata().Should().BeEquivalentTo(expected);
    }
}