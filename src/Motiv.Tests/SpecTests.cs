namespace Motiv.Tests;

public class SpecTests
{
    public class TestSpec() : Spec<bool, string>(
        Spec.Build(UnderlyingSpec)
            .WhenTrue(True)
            .WhenFalse(False)
            .Create(PrimaryStatement))
    {
        public const string True = "true";
        public const string False = "false";
        public const string UnderlyingStatement = "underlying";
        public const string PrimaryStatement = "is true";

        public static SpecBase<bool, string> UnderlyingSpec => Spec
            .Build((bool b) => b)
            .Create(UnderlyingStatement);
    }


    public class TestFromFactorySpec() : Spec<bool, string>(() =>
        Spec.Build(UnderlyingSpec)
            .WhenTrue(True)
            .WhenFalse(False)
            .Create(PrimaryStatement))
    {
        public const string True = "true";
        public const string False = "false";
        public const string UnderlyingStatement = "underlying";
        public const string PrimaryStatement = "is true";

        public static SpecBase<bool, string> UnderlyingSpec => Spec
            .Build((bool b) => b)
            .Create(UnderlyingStatement);
    }

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
        act.ShouldBe(model);
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
        var act = result.Values;

        // Assert
        act.ShouldBe([model.ToString()]);
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
        act.ShouldBe(model);
    }

    [Theory]
    [InlineAutoData(true, "underlying true")]
    [InlineAutoData(false, "underlying false")]
    public void Should_return_metadata(
        bool model,
        string metadata)
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
        var act = result.Values;

        // Assert
        act.ShouldBe([metadata]);
    }


    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_yield_the_underlying_proposition_results(bool model)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build(underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Underlying.First().Description.Statement;

        // Assert
        act.ShouldBe("underlying");
    }

    [Fact]
    public void Should_yield_the_underlying_propositions()
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build(underlyingSpec)
            .WhenTrue("underlying true")
            .WhenFalse("underlying false")
            .Create("is true");

        // Act
        var act = spec.Underlying.First().Name;

        // Assert
        act.ShouldBe("underlying");
    }

    [Fact]
    public void Should_yield_underlying_from_custom_spec_class()
    {
        // Arrange
        var spec = new TestFromFactorySpec();

        // Act
        var act = spec.Underlying.First().Name;

        // Assert
        act.ShouldBe(TestSpec.UnderlyingStatement);
    }

    [Fact]
    public void Should_yield_statement_from_custom_spec_class()
    {
        // Arrange
        var spec = new TestSpec();

        // Act
        var act = spec.Name;

        // Assert
        act.ShouldBe(TestSpec.PrimaryStatement);
    }

    [Theory]
    [InlineAutoData(true, TestSpec.True)]
    [InlineAutoData(false, TestSpec.False)]
    public void Should_evaluate_a_custom_spec_results(bool model, string expected)
    {
        // Arrange
        var spec = new TestSpec();
        var results = spec.IsSatisfiedBy(model);

        // Act
        var act = results.Values.First();

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, "true")]
    [InlineAutoData(false, "false")]
    public void Should_return_the_underlying_assertions_as_metadata(bool model, string metadata)
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
        act.ShouldBe([metadata]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_the_underlying_metadata(bool model, object metadata)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(metadata)
            .WhenFalse(metadata)
            .Create("is model value");

        var spec = Spec
            .Build(() => underlyingSpec)
            .WhenTrue(new object())
            .WhenFalse(new object())
            .Create("is true");

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.MetadataTier.Underlying.SelectMany(m => m.Metadata);

        // Assert
        act.ShouldBe([metadata]);
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
        act.ShouldBe([expectedAssertion]);
    }

    [Theory]
    [InlineAutoData(true, "underlying true")]
    [InlineAutoData(false, "underlying false")]
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
        act.ShouldBe(expectedDescription);
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
        act.ShouldNotThrow();
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
        act.ShouldBe(model);
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
        var act = result.Values;

        // Assert
        act.ShouldBe([model]);
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
        act.ShouldNotThrow();
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
        act.ShouldThrow<ArgumentNullException>();
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
        act.ShouldBe("is null");
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
        act.ShouldBe(
            """
            top-level proposition
                is null
            """);
    }

    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Statement_property_during_and_operation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => true).Create("a");
        var specB = Spec.Build((bool _) => false).Create("b");
        var specC = Spec.Build((bool _) => false).Create("c");

        // Compose new proposition
        var sut = specA & !(specB | specC);

        var act = sut.Name;

        act.ShouldBe("a & !(b | c)");
    }

    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Statement_property_during_and_operation_with_triple_negation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => true).Create("a");
        var specB = Spec.Build((bool _) => false).Create("b");
        var specC = Spec.Build((bool _) => false).Create("c");

        // Compose new proposition
        var sut = specA & !!!(specB | specC);

        var act = sut.Name;

        act.ShouldBe("a & !(b | c)");
    }

    [Fact]
    public void Should_ensure_that_brackets_are_applied_correctly_with_the_Statement_property_during_and_operation_with_no_negation()
    {
        // Define clauses
        var specA = Spec.Build((bool _) => true).Create("a");
        var specB = Spec.Build((bool _) => false).Create("b");
        var specC = Spec.Build((bool _) => false).Create("c");

        // Compose new proposition
        var sut = specA & (specB | specC);

        var act = sut.Name;

        act.ShouldBe("a & (b | c)");
    }

    [Fact]
    public void Should_evaluate_specs_that_don_not_have_a_metadata_defined_and_assume_metadata_is_a_string()
    {
        // Arrange
        SpecBase<bool> sut = Spec.Build((bool _) => true).Create("is true");

        // Act
        var act = sut.IsSatisfiedBy(false);

        // Assert
        act.Values.ShouldBe(["is true == true"]);
    }

    [Fact]
    public void Should_evaluate_metadata_specs_that_have_a_metadata_defined_and_assume_metadata_is_a_string()
    {
        // Arrange
        SpecBase<bool> sut = Spec.Build((bool _) => true)
            .WhenTrue(new object())
            .WhenFalse(new object())
            .Create("is true");

        // Act
        var act = sut.IsSatisfiedBy(false);

        // Assert
        act.Values.ShouldBe(["is true == true"]);
    }
}

