using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class SpecTests
{
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_a_result_that_satisfies_the_predicate(bool model)
    {
        var sut = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns model value");

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(model);
        result.MetadataTree.Should().ContainSingle(model.ToString());
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_a_result_that_satisfies_the_spec(bool model)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();
        
        var sut = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(model);
    }
    
    [Theory]
    [InlineAutoData(true, "underlying true", "is true")]
    [InlineAutoData(false, "underlying false", "!is true")]
    public void Should_return_a_result_that_explains_the_result(bool model, string expectedAssertion, string expectedDescription)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();
        
        var sut = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = sut.IsSatisfiedBy(model);
        
        result.MetadataTree.Should().ContainSingle(expectedAssertion);
        result.MetadataTree.Underlying.SelectMany(metadataTree => metadataTree).Should().BeEquivalentTo(model.ToString());
        result.Explanation.Assertions.Should().BeEquivalentTo(expectedAssertion);
        result.Description.Compact.Should().Be(expectedDescription);
    }

    [Fact]
    public void Should_handle_null_model_without_throwing()
    {
        var sut = Spec
            .Build<string?>(m => m is null)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is null");

        var result = sut.IsSatisfiedBy(null);

        result.Satisfied.Should().BeTrue();
        result.MetadataTree.Should().ContainSingle(true.ToString());
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_a_result_that_satisfies_the_predicate_when_using_textual_specification(bool model)
    {
        var sut = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(model);
        result.MetadataTree.Should().ContainSingle(model.ToString());
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_allow_change_of_metadata_from_spec_creation_from_existing_spec(bool model)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();
        
        var sut = Spec
            .Build(underlyingSpec)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("new spec");

        var result = sut.IsSatisfiedBy(model);

        result.Satisfied.Should().Be(model);
        result.MetadataTree.Should().ContainSingle(model.ToString());
    }

    [Fact]
    public void Should_handle_null_model_without_throwing_when_using_textual_specification()
    {
        var sut = Spec
            .Build<string?>(m => m is null)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var result = sut.IsSatisfiedBy(null);

        result.Satisfied.Should().Be(true);
        result.MetadataTree.Should().ContainSingle(true.ToString());
    }

    [Fact]
    public void Should_throw_if_null_predicate_is_supplied()
    {
        var act = () => Spec
            .Build<string?>(default(Func<string, bool>)!)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineAutoData("true", null)]
    [InlineAutoData(null, "false")]
    public void Should_throw_if_null_metadata_supplied(string? trueMetadata, string? falseMetadata)
    {
        var act = () => Spec
            .Build<string?>(m => m is null)
            .WhenTrue(trueMetadata!)
            .WhenFalse(falseMetadata!)
            .Create("is null");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineAutoData("hello world", null)]
    [InlineAutoData(null, "hello world")]
    public void Should_throw_if_invalid_reasons_are_supplied(string? trueBecause, string? falseBecause)
    {
        var act = () => Spec
            .Build<string?>(m => m is null)
            .WhenTrue(trueBecause!)
            .WhenFalse(falseBecause!)
            .Create();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception_when_using_text_metadata()
    {
        var act = () => Spec
            .Build((Func<string?, bool>)(_ => throw new Exception("should be wrapped")))
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create()
            .IsSatisfiedBy(null);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>();
        act.Should().Throw<SpecException>().WithMessage("*should be wrapped*");
        act.Should().Throw<SpecException>().WithMessage($"*{true}*");
    }

    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception()
    {
        var spec = Spec
            .Build((Func<string?, bool>)(_ => throw new Exception("should be wrapped")))
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("should throw");

        var act = () => spec.IsSatisfiedBy(null);

        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>();
        act.Should().Throw<SpecException>().WithMessage("*should be wrapped*");
        act.Should().Throw<SpecException>().WithMessage("*should throw*");
    }

    [Fact]
    public void Should_provide_detailed_proposition()
    {
        var sut = Spec
            .Build<object?>(m => m is null)
            .WhenTrue("is null")
            .WhenFalse("is not null")
            .Create();

        var act = sut.Proposition.Detailed;

        act.Should().Be("is null");
    }

    [Fact]
    public void Should_provide_detailed_proposition_when_spec_is_composed_of_an_underlying_sepc()
    {
        var underlying = Spec
            .Build<object?>(m => m is null)
            .WhenTrue("is null")
            .WhenFalse("is not null")
            .Create();
        
        var sut = Spec
            .Build(underlying)
            .Create("top-level proposition");

        var act = sut.Proposition.Detailed;

        act.Should().Be(
            """
            top-level proposition {
                is null
            }
            """);
    }
}