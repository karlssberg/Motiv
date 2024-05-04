namespace Motiv.Tests;

public class SpecTests
{
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_satisfy_the_predicate(bool model)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns model value");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(model);
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_yield_metadata(bool model)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns model value");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().ContainSingle(model.ToString());
    }
    
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_a_result_that_satisfies_the_spec(bool model)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();
        
        var spec = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(model);
    }
    
    [Theory]
    [InlineAutoData(true, "underlying true")]
    [InlineAutoData(false, "underlying false")]
    public void Should_return_metadata(
        bool model,
        string expectedAssertion)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();
        
        var spec = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().ContainSingle(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(true, "true")]
    [InlineAutoData(false, "false")]
    public void Should_return_the_underlying_metadata(bool model, string metadata)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(metadata)
            .WhenFalse(metadata)
            .Create("is model value");
        
        var spec = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.MetadataTier.Underlying.SelectMany(m => m.Metadata);
        
        // Assert
        act.Should().BeEquivalentTo(metadata);
    }

    [Theory]
    [InlineAutoData(true, "underlying true")]
    [InlineAutoData(false, "underlying false")]
    public void Should_return_the_assertions(
        bool model,
        string expectedAssertion)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();
        
        var spec = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "!is true")]
    public void Should_return_a_result_that_explains_the_result(bool model,string expectedDescription)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();
        
        var spec = Spec
            .Build(() => underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Description.Reason;
        
        // Assert
        act.Should().Be(expectedDescription);
    }

    [Fact]
    public void Should_handle_null_model_without_throwing()
    {
        // Arrange
        var spec = Spec
            .Build<string?>(m => m is null)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is null");

        // Act
        var act = () => spec.IsSatisfiedBy(null);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_a_result_that_satisfies_the_predicate_when_using_textual_specification(bool model)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(model);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_allow_change_of_metadata_from_spec_creation_from_existing_spec(bool model)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();
        
        var spec = Spec
            .Build(underlyingSpec)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("new spec");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().ContainSingle(model.ToString());
    }

    [Fact]
    public void Should_not_throw_if_null_is_supplied_as_a_model()
    {
        // Arrange
        var spec = Spec.Build<string?>(m => m is null)
                       .WhenTrue(true.ToString())
                       .WhenFalse(false.ToString())
                       .Create();
        
        // Act
        var act = () => spec.IsSatisfiedBy(null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Should_throw_if_null_predicate_is_supplied()
    {
        // Act
        var act = () => Spec
            .Build(default(Func<string, bool>)!)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineAutoData("true", null)]
    [InlineAutoData(null, "false")]
    public void Should_throw_if_null_metadata_supplied(string trueMetadata, string falseMetadata)
    {
        // Arrange
        // Act
        var act = () => Spec
            .Build((string _) => default)
            .WhenTrue(trueMetadata)
            .WhenFalse(falseMetadata)
            .Create("is null");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineAutoData("hello world", null)]
    [InlineAutoData(null, "hello world")]
    public void Should_throw_if_null_reasons_are_supplied(string trueBecause, string falseBecause)
    {
        // Act
        var act = () => Spec
            .Build<string>(m => m is null)
            .WhenTrue(trueBecause)
            .WhenFalse(falseBecause)
            .Create();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception_when_using_text_metadata()
    {
        // Arrange
        var spec = Spec
            .Build((Func<string?, bool>)(_ => throw new Exception("should be wrapped")))
            .WhenTrue("should be used in exception message")
            .WhenFalse("should not be used in exception message")
            .Create();
        
        // Act
        var act = () => spec.IsSatisfiedBy(null);

        // Assert
        act.Should().Throw<SpecException>()
            .WithMessage(
                "*An 'Exception' was thrown with the message 'should be wrapped' " +
                "while evaluating the 'predicate' parameter that was supplied to Spec<String> named 'should be used in exception message'.*")
            .WithInnerExceptionExactly<Exception>();
    }

    [Fact]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception()
    {
        // Arrange
        const string statement = "should be used in exception message";
        
        var spec = Spec
            .Build((Func<string?, bool>)(_ => throw new Exception("should be wrapped")))
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create(statement);

        // Act
        var act = () => spec.IsSatisfiedBy(null);

        // Assert
        act.Should().Throw<SpecException>()
            .WithMessage(
                "*An 'Exception' was thrown with the message 'should be wrapped' " +
                "while evaluating the 'predicate' parameter that was supplied to Spec<String> named 'should be used in exception message'.*")
            .WithInnerExceptionExactly<Exception>();
    }

    [Fact]
    public void Should_provide_detailed_proposition()
    {
        // Arrange
        var spec = Spec
            .Build<object?>(m => m is null)
            .WhenTrue("is null")
            .WhenFalse("is not null")
            .Create();

        // Act
        var act = spec.Expression;

        // Assert
        act.Should().Be("is null");
    }

    [Fact]
    public void Should_provide_detailed_proposition_when_spec_is_composed_of_an_underlying_sepc()
    {
        // Arrange
        var underlying = Spec
            .Build<object?>(m => m is null)
            .WhenTrue("is null")
            .WhenFalse("is not null")
            .Create();
        
        var spec = Spec
            .Build(underlying)
            .Create("top-level proposition");

        // Act
        var act = spec.Expression;

        // Assert
        act.Should().Be(
            """
            top-level proposition
                is null
            """);
    }
}