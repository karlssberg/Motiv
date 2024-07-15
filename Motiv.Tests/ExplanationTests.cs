namespace Motiv.Tests;

public class ExplanationTests
{
    public enum NumberType
    {
        Even,
        Odd
    }

    private class IsEvenWholeNumber() : Spec<int>(() =>
    {
        // An overly complex specification to help catch composition issues
        var isEvenSpecAtRootDepth =
            Spec.Build((long n) => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpecAtRootDepth =
            Spec.Build((decimal n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();

        var isEvenWrapperSpecAtDepth2 =
            Spec.Build(isEvenSpecAtRootDepth)
                .WhenTrue("even wrapper")
                .WhenFalse("odd wrapper")
                .Create();

        var isPositiveWrapperSpecAtDepth2 =
            Spec.Build(isPositiveSpecAtRootDepth)
                .WhenTrue("positive wrapper")
                .WhenFalse("not positive wrapper")
                .Create();

        var isEvenAndPositiveUsingPredicateSpecAtDepth1 =
            Spec.Build((int n) =>
                    isEvenWrapperSpecAtDepth2.IsSatisfiedBy(n) & isPositiveWrapperSpecAtDepth2.IsSatisfiedBy(n))
                .WhenTrue("even and positive from predicate")
                .WhenFalse("not even and positive from predicate")
                .Create();

        var isEvenAndPositiveUsingChangeModelSpecAtDepth1 =
            Spec.Build((int n) =>
                    (isEvenWrapperSpecAtDepth2.ChangeModelTo<int>(i => i) &
                     isPositiveWrapperSpecAtDepth2.ChangeModelTo<int>(i => i))
                    .IsSatisfiedBy(n))
                .WhenTrue("even and positive from change model method")
                .WhenFalse("not even and positive from change model method")
                .Create();

        var isEvenWholeNumber =
            Spec.Build(isEvenAndPositiveUsingPredicateSpecAtDepth1 & isEvenAndPositiveUsingChangeModelSpecAtDepth1)
                .WhenTrue("even whole number")
                .WhenFalse("not an even whole number")
                .Create();
        return isEvenWholeNumber;
    });

    [Theory]
    [InlineAutoData(1, "is odd")]
    [InlineAutoData(2, "is even")]
    public void Should_provide_a_reason_for_a_spec_result(int n, string expected)
    {
        // Arrange
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();

        var result = spec.IsSatisfiedBy(n);

        // Act
        var act = result.Explanation.Assertions;

        // Assert
        act.Should().ContainSingle(expected);
    }

    [Theory]
    [InlineData(2, "even whole number")]
    [InlineData(0, "not an even whole number")]
    public void Should_assert_complex_spec(int model, string expected)
    {
        // Arrange
        var isEvenWholeNumber = new IsEvenWholeNumber();

        var result = isEvenWholeNumber.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(2, "even", "positive")]
    [InlineData(3, "odd", "positive")]
    [InlineData(0, "even", "not positive")]
    [InlineData(-3, "odd", "not positive")]
    public void Should_yield_all_root_assertions_for_complex_spec(int model, params string[] expected)
    {
        // Arrange
        var isEvenWholeNumber = new IsEvenWholeNumber();

        var result = isEvenWholeNumber.IsSatisfiedBy(model);

        // Act
        var act = result.AllRootAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }



    [Theory]
    [InlineData(2, "even", "positive")]
    [InlineData(3, "odd")]
    [InlineData(0, "not positive")]
    [InlineData(-3, "odd", "not positive")]
    public void Should_yield_causal_root_assertions_for_complex_spec(int model, params string[] expected)
    {
        // Arrange
        var isEvenWholeNumber = new IsEvenWholeNumber();

        var result = isEvenWholeNumber.IsSatisfiedBy(model);

        // Act
        var act = result.RootAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(2, "even and positive from predicate", "even and positive from change model method")]
    [InlineData(3, "not even and positive from predicate", "not even and positive from change model method")]
    [InlineData(-2, "not even and positive from predicate", "not even and positive from change model method")]
    [InlineData(-3, "not even and positive from predicate", "not even and positive from change model method")]
    public void Should_yield_causal_sub_assertions_for_complex_spec(int model, params string[] expected)
    {
        // Arrange
        var isEvenWholeNumber = new IsEvenWholeNumber();

        var result = isEvenWholeNumber.IsSatisfiedBy(model);

        // Act
        var act = result.SubAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }


    [Theory]
    [InlineData(6, "even", "positive", "divisible by 3")]
    [InlineData(4, "even", "positive", "not divisible by 3")]
    [InlineData(3, "odd", "positive", "divisible by 3")]
    [InlineData(1, "odd", "positive", "not divisible by 3")]
    [InlineData(0, "even", "not positive", "divisible by 3")]
    [InlineData(-3, "odd", "not positive", "divisible by 3")]
    [InlineData(-5, "odd", "not positive", "not divisible by 3")]
    public void Should_yeild_the_assertions_from_all_operands_when_using_all_assertions_property(int n, params string[] expected)
    {
        // Arrange
        var isEvenSpec =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpec =
            Spec.Build((int i) => i > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();

        var isDivisibleBy3Spec =
            Spec.Build((int i) => i % 3 == 0)
                .WhenTrue("divisible by 3")
                .WhenFalse("not divisible by 3")
                .Create();

        var spec = isEvenSpec & isPositiveSpec & isDivisibleBy3Spec;

        var result = spec.IsSatisfiedBy(n);

        // Act
        var act = result.AllAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(6, "even", "positive", "divisible by 3")]
    [InlineData(4, "not divisible by 3")]
    [InlineData(3, "odd")]
    [InlineData(1, "odd", "not divisible by 3")]
    [InlineData(0, "not positive")]
    [InlineData(-3, "odd", "not positive")]
    [InlineData(-5, "odd", "not positive", "not divisible by 3")]
    public void Should_yield_the_assertions_from_determinate_operands_when_using_sub_assertions(
        int n,
        params string[] expected)
    {
        // Arrange
        var isEvenSpec =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpec =
            Spec.Build((int i) => i > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();

        var isDivisibleBy3Spec =
            Spec.Build((int i) => i % 3 == 0)
                .WhenTrue("divisible by 3")
                .WhenFalse("not divisible by 3")
                .Create();

        var spec =
            Spec.Build(isEvenSpec & isPositiveSpec & isDivisibleBy3Spec)
                .WhenTrue("even, positive, and divisible by 3")
                .WhenFalse("not even, positive, or divisible by 3")
                .Create();

        var result = spec.IsSatisfiedBy(n);

        // Act
        var act = result.SubAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(6, "even", "positive", "divisible by 3")]
    [InlineData(4, "even", "positive", "not divisible by 3")]
    [InlineData(3, "odd", "positive", "divisible by 3")]
    [InlineData(1, "odd", "positive", "not divisible by 3")]
    [InlineData(0, "even", "not positive", "divisible by 3")]
    [InlineData(-3, "odd", "not positive", "divisible by 3")]
    [InlineData(-5, "odd", "not positive", "not divisible by 3")]
    public void Should_yeild_the_assertions_from_determinate_operands_when_using_all_sub_assertions(
        int n,
        params string[] expected)
    {
        // Arrange
        var isEvenSpec =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpec =
            Spec.Build((int i) => i > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();

        var isDivisibleBy3Spec =
            Spec.Build((int i) => i % 3 == 0)
                .WhenTrue("divisible by 3")
                .WhenFalse("not divisible by 3")
                .Create();

        var spec =
            Spec.Build(isEvenSpec & isPositiveSpec & isDivisibleBy3Spec)
                .WhenTrue("even, positive, and divisible by 3")
                .WhenFalse("not even, positive, or divisible by 3")
                .Create();

        var result = spec.IsSatisfiedBy(n);

        // Act
        var act = result.AllSubAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(1, "odd")]
    [InlineData(2, "even")]
    public void Should_forward_assertions_when_using_basic_explanation_propositions(int model, string expected)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isEvenWrapper1 =
            Spec.Build(isEven)
                .Create("is even wrapper");

        var isEvenWrapper2 =
            Spec.Build(isEvenWrapper1).WhenTrueYield<string>((_, result) => result.Assertions)
                .WhenFalseYield((_, result) => result.Assertions)
                .Create("is even wrapper 2");

        var result = isEvenWrapper2.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(1, "odd")]
    [InlineData(2, "even")]
    public void Should_forward_assertions_as_metadata_when_using_basic_explanation_propositions(int model, string expected)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isEvenWrapper1 =
            Spec.Build(isEven)
                .Create("is even wrapper");

        var isEvenWrapper2 =
            Spec.Build(isEvenWrapper1)
                .WhenTrueYield((_, result) => result.Assertions)
                .WhenFalseYield((_, result) => result.Assertions)
                .Create("is even wrapper 2");

        var result = isEvenWrapper2.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(1, NumberType.Odd)]
    [InlineData(2, NumberType.Even)]
    public void Should_forward_assertions_when_using_basic_metadata_propositions(
        int model,
        NumberType expectedMetadata)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(NumberType.Even)
                .WhenFalse(NumberType.Odd)
                .Create("is even");

        var isEvenWrapper1 =
            Spec.Build(isEven)
                .Create("is even wrapper");

        var isEvenWrapper2 =
            Spec.Build(isEvenWrapper1)
                .WhenTrueYield((_, result) => result.Values)
                .WhenFalseYield((_, result) => result.Values)
                .Create("is even wrapper 2");

        var result = isEvenWrapper2.IsSatisfiedBy(model);

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo([expectedMetadata]);
    }

    [Theory]
    [InlineData(1, "¬is even wrapper 2")]
    [InlineData(2, "is even wrapper 2")]
    public void Should_use_propositional_statement_as_assertions_when_using_basic_metadata_propositions(
        int model,
        string expectedAssertion)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(NumberType.Even)
                .WhenFalse(NumberType.Odd)
                .Create("is even");

        var isEvenWrapper1 =
            Spec.Build(isEven)
                .Create("is even wrapper");

        var isEvenWrapper2 =
            Spec.Build(isEvenWrapper1)
                .WhenTrueYield((_, result) => result.Values)
                .WhenFalseYield((_, result) => result.Values)
                .Create("is even wrapper 2");

        var result = isEvenWrapper2.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo([expectedAssertion]);
    }

    [Theory]
    [InlineData(2, "even")]
    [InlineData(3, "odd")]
    public void Should_not_have_duplicate_assertions_in_explanation(int model, string expected)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var allEven =
            Spec.Build(isEven)
                .AsAllSatisfied()
                .Create("all even");

        var firstEven =
            Spec.Build(allEven)
                .Create("first even");

        var secondEven =
            Spec.Build(firstEven)
                .WhenTrueYield((_, result) => result.Assertions)
                .WhenFalseYield((_, result) => result.Assertions)
                .Create("second even");

        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrueYield((_, result) => result.Values)
                .WhenFalseYield((_, result) => result.Values)
                .Create("third even");

        var result = thirdEven.IsSatisfiedBy([model]);

        // Act
        var act = result.Explanation.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(2, "even")]
    [InlineData(3, "odd")]
    public void Should_allow_duplicate_underlying_explanations(int model, string expected)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var allEven =
            Spec.Build(isEven)
                .AsAllSatisfied()
                .Create("all even");

        var firstEven =
            Spec.Build(allEven)
                .Create("first even");

        var secondEven =
            Spec.Build(firstEven)
                .WhenTrueYield((_, result) => result.Assertions)
                .WhenFalseYield((_, result) => result.Assertions)
                .Create("second even");

        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrueYield((_, result) => result.Values)
                .WhenFalseYield((_, result) => result.Values)
                .Create("third even");

        var result = thirdEven.IsSatisfiedBy([model]);

        // Act
        var act = result.Explanation.Underlying.GetAssertions();

        // Assert
        act.Should().NotBeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Should_ensure_there_are_no_superfluous_descendants_of_explanations(int model)
    {
        // Arrange
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var allEven =
            Spec.Build(isEven)
                .AsAllSatisfied()
                .Create("all even");

        var firstEven =
            Spec.Build(allEven)
                .Create("first even");

        var secondEven =
            Spec.Build(firstEven)
                .WhenTrueYield((_, result) => result.Assertions)
                .WhenFalseYield((_, result) => result.Assertions)
                .Create("second even");

        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrueYield((_, result) => result.Values)
                .WhenFalseYield((_, result) => result.Values)
                .Create("third even");

        var result = thirdEven.IsSatisfiedBy([model]);

        // Act
        var act = result.Explanation.Underlying.SelectMany(explanation => explanation.Underlying);

        // Assert
        act.Should().BeEmpty();
    }
}
