using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ExplanationSpecTests
{
    [Theory]
    [InlineData(1, "is odd")]
    [InlineData(2, "is even")]
    public void Should_allow_the_creation_of_specs_with_custom_textual_metadata(int model, string expected)
    {
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();
        
        var result = spec.IsSatisfiedBy(model);
        result.Assertions.Should().BeEquivalentTo(expected);
        result.Reason.Should().Be(expected);
        result.ExplanationTree.Assertions.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(1, "!is even")]
    [InlineData(2, "is even")]
    public void Should_allow_a_proposition_that_provides_a_reason_for_a_spec_result(int model, string expected)
    {
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("even")
            .WhenFalse("odd")
            .Create("is even");
        
        var result = spec.IsSatisfiedBy(model);
        result.Reason.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(1, "odd")]
    [InlineData(2, "even")]
    public void Should_use_string_metadata_as_a_proposition_that_provides_a_reason_for_a_spec_result(int model, string expected)
    {
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("even")
            .WhenFalse("odd")
            .Create();
        
        var result = spec.IsSatisfiedBy(model);
        result.Reason.Should().Be(expected);
        result.Assertions.Should().BeEquivalentTo(expected);
    }
}