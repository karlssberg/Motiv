using FluentAssertions;
using Karlssberg.Motive.All;

namespace Karlssberg.Motive.Tests;

public class ChangeModelSpecificationTests
{
    
    [Theory]
    [AutoParams("hello", false)]
    [AutoParams(default(string), true)]
    public void Should_change_the_model(string? model, bool expected)
    {
        var isEmpty = new Specification<object?>(o => o is null, "is null", "is not null");
        var sut = isEmpty.ChangeModel<string?>();
            
        var act = sut.Evaluate(model);
        
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
        var isValueType = new Specification<Type>(t => t.IsValueType, "is value-type", "is not value-type");
        var sut = isValueType.ChangeModel<object>(m => m.GetType());
        
        var act = sut.Evaluate(model);
        
        act.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams("hello", true)]
    [AutoParams("world", true)]
    [AutoParams("h1e234j", false)]
    public void Should_logically_resolve_parent_specification(string model, bool expected)
    {          
        
        var isLetter = new Specification<char>(
            "is a letter",
            char.IsLetter, 
            ch => $"'{ch}' is a letter", 
            ch => $"'{ch}' is not a letter");
        
        var isAllLetters = isLetter
            .All()
            .ChangeModel<string>(m => m.ToCharArray());
        
        var act = isAllLetters.Evaluate(model);
        
        act.IsSatisfied.Should().Be(expected);
    }
    
    [Theory]
    [AutoParams("#hello", "'#' is not a letter")]
    [AutoParams("!world!", "'!' is not a letter")]
    [AutoParams("ok", "'o' is a letter", "'k' is a letter")]
    public void Should_identify_non_letters(string model, params string[] expected)
    {          
        var isLetter = new Specification<char>(
            "is a letter",
            char.IsLetter, 
            ch => $"'{ch}' is a letter", 
            ch => $"'{ch}' is not a letter");
        
        var isAllLetters = isLetter
            .All()
            .ChangeModel<string>(m => m.ToCharArray());
        
        var act = isAllLetters.Evaluate(model);
        
        act.Causes.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied_when_not_using_model_selector_function(
        string? trueMetadata, 
        string? falseMetadata,
        string? model)
    {
        var spec = new Specification<object?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        
        var act = () =>
        {
            var sut = spec.ChangeModel<string?>();
            sut.Evaluate(model);
        };

        act.Should().NotThrow();
    }
    
    
    [Theory]
    [AutoParams("true",  null)]
    [AutoParams(null, "false")]
    public void Should_not_throw_if_null_metadata_supplied_when_using_model_selector_function(
        string? trueMetadata, 
        string? falseMetadata,
        object? model)
    {
        var spec = new Specification<string?, string?>(
            "is null",
            m => m is null,
            trueMetadata,
            falseMetadata);
        
        
        var act = () =>
        {
            var sut = spec.ChangeModel<object?>(obj => obj?.ToString());
            sut.Evaluate(model);
        };

        act.Should().NotThrow();
    }
}