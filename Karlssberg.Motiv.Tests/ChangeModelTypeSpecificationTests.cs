using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ChangeModelTypeSpecificationTests
{
    
    [Theory]
    [AutoParams("hello", false)]
    [AutoParams(default(string), true)]
    public void Should_change_the_model(string? model, bool expected)
    {
        var isEmpty = Spec
            .Build<object?>(m => m is null)
            .YieldWhenTrue("is null")
            .YieldWhenFalse("is not null")
            .CreateSpec();
            
        var sut = isEmpty.ChangeModel<string?>();
            
        var act = sut.IsSatisfiedBy(model);
        
        act.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams("hello", false)]
    [AutoParams(1, true)]
    [AutoParams(1d, true)]
    [AutoParams(false, true)]
    [AutoParams(typeof(object), false)]
    public void Should_change_the_model_using_a_model_selector_function(object model, bool expected)
    {
        var isValueType = Spec
            .Build<Type>(t => t.IsValueType)
            .YieldWhenTrue("is value-type")
            .YieldWhenFalse("is not value-type")
            .CreateSpec();
            
        var sut = isValueType.ChangeModel<object>(m => m.GetType());
        
        var act = sut.IsSatisfiedBy(model);
        
        act.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams("hello", true)]
    [AutoParams("world", true)]
    [AutoParams("h1e234j", false)]
    public void Should_logically_resolve_parent_specification(string model, bool expected)
    {
        var isLetter = Spec
            .Build<char>(char.IsLetter)
            .YieldWhenTrue(ch => $"'{ch}' is a letter")
            .YieldWhenFalse(ch => $"'{ch}' is not a letter")
            .CreateSpec("is a letter");
        
        var isAllLetters = isLetter
            .ToAllSatisfiedSpec()
            .ChangeModel<string>(m => m.ToCharArray());
        
        var act = isAllLetters.IsSatisfiedBy(model);
        
        act.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams("#hello", "'#' is not a letter")]
    [AutoParams("!world!", "'!' is not a letter")]
    [AutoParams("ok", "'o' is a letter", "'k' is a letter")]
    public void Should_identify_non_letters(string model, params string[] expected)
    {          
        var isLetter = Spec
            .Build<char>(char.IsLetter)
            .YieldWhenTrue(ch => $"'{ch}' is a letter")
            .YieldWhenFalse(ch => $"'{ch}' is not a letter")
            .CreateSpec("is a letter");
        
        var isAllLetters = isLetter
            .ToAllSatisfiedSpec()
            .ChangeModel<string>(m => m.ToCharArray());
        
        var act = isAllLetters.IsSatisfiedBy(model);
        
        act.GetInsights().Should().BeEquivalentTo(expected);
    }
}