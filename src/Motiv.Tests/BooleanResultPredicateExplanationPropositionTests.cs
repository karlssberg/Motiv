namespace Motiv.Tests;

public class BooleanResultPredicateExplanationPropositionTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_policy_result_predicate(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var fifthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue((_, _) => "fifth is true")
            .WhenFalse("fifth is false")
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("sixth is true")
            .WhenFalse((_, _) => "sixth is false")
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue((_, _) => "seventh is true")
            .WhenFalse((_, _) => "seventh is false")
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_boolean_result_predicate(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var fifthSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue((_, _) => "fifth is true")
            .WhenFalse("fifth is false")
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("sixth is true")
            .WhenFalse((_, _) => "sixth is false")
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue((_, _) => "seventh is true")
            .WhenFalse((_, _) => "seventh is false")
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_policy_result_predicate_when_yielding_multiple_assertions(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("second is true")
            .WhenFalseYield((_, _) => ["second is false"])
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["first is true"])
            .WhenFalseYield((_, _) => ["second is false"])
            .Create("is third true");

        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_be_satisfied_boolean_result_predicate_when_yielding_multiple_assertions(
        bool model,
        bool other,
        bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("second is true")
            .WhenFalseYield((_, _) => ["second is false"])
            .Create("is second true");

        var thirdSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["first is true"])
            .WhenFalseYield((_, _) => ["second is false"])
            .Create("is third true");

        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, "are equal == true")]
    [InlineData(false, true, "are equal == false")]
    [InlineData(true, false, "are equal == false")]
    [InlineData(true, true, "are equal == true")]
    public void Should_provide_root_assertions(
        bool model,
        bool other,
        string expectedRootAssertion)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var fifthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue((_, _) => "fifth is true")
            .WhenFalse("fifth is false")
            .Create("is fifth true");

        var sixthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("sixth is true")
            .WhenFalse((_, _) => "sixth is false")
            .Create("is sixth true");

        var seventhSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue((_, _) => "seventh is true")
            .WhenFalse((_, _) => "seventh is false")
            .Create("is seventh true");

        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec | fifthSpec | sixthSpec | seventhSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.RootAssertions;

        // Assert
        act.ShouldBe([expectedRootAssertion]);
    }

    [Theory]
    [InlineData(true,  "first is true", "second is true", "third is true", "fourth is true")]
    [InlineData(false,  "first is false", "second is false", "third is false", "fourth is false")]
    public void Should_replace_policy_result_assertion_with_new_assertion(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");


        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expectedAssertions);
    }


    [Theory]
    [InlineData(true,  "first is true", "is second true == true", "is third true == true", "is fourth true == true")]
    [InlineData(false,  "first is false", "is second true == false", "is third true == false", "is fourth true == false")]
    public void Should_replace_boolean_result_assertion_with_new_assertion(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");


        var spec = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true,  "first is true", "second is true", "third is true")]
    [InlineData(false, "first is false", "second is false", "third is false")]
    public void Should_replace_policy_result_assertion_with_new_assertion_when_yielding_multiple_assertions(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrue("second is true")
            .WhenFalseYield((_, _) => ["second is false"])
            .Create("second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["third is true"])
            .WhenFalseYield((_, _) => ["third is false"])
            .Create("third true");


        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true,  "first true == true", "second true == true", "third true == true")]
    [InlineData(false, "first true == false", "second true == false", "third true == false")]
    public void Should_replace_boolean_result_assertion_with_new_assertion_when_yielding_multiple_assertions(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");

        var firstSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrue("second is true")
            .WhenFalseYield((_, _) => ["second is false"])
            .Create("second true");

        var thirdSpec = Spec
            .Build(BooleanResultBase<string> (bool m) => underlying.Evaluate(m))
            .WhenTrueYield((_, _) => ["third is true"])
            .WhenFalseYield((_, _) => ["third is false"])
            .Create("third true");


        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Should_derive_the_reason_from_assertions_for_policy_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true")
                .WhenFalse("false")
                .Create();

        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true")
                .WhenFalse(_ => "false")
                .Create();

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true")
                .WhenFalse((_, _) => "false")
                .Create();

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }


    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Should_derive_the_reason_from_assertions_for_boolean_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true")
                .WhenFalse("false")
                .Create();

        var withFalseAsParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true")
                .WhenFalse(_ => "false")
                .Create();

        var withFalseAsTwoParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true")
                .WhenFalse((_, _) => "false")
                .Create();

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "(propositional statement == true) & (propositional statement == true) & (propositional statement == true)")]
    [InlineData(false, "(propositional statement == false) & (propositional statement == false) & (propositional statement == false)")]
    public void Should_use_the_propositional_statement_in_the_reason_for_policy_results(
        bool model,
        string expectedReason)
    {
        // Arrange

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_for_boolean_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_more_than_one_assertion_possible_for_policy_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }


    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_more_than_one_assertion_possible_for_boolean_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "true assertion == true")]
    [InlineData(false, "true assertion == false")]
    public void Should_use_the_implicit_propositional_statement_in_the_reason_when_more_than_one_assertion_possible_for_policy_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create();

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "true assertion == true")]
    [InlineData(false, "true assertion == false")]
    public void Should_use_the_implicit_propositional_statement_in_the_reason_when_more_than_one_assertion_possible_for_boolean_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue("true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create();

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_policy_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "(propositional statement == true) & (propositional statement == true) & (propositional statement == true)")]
    [InlineData(false, "(propositional statement == false) & (propositional statement == false) & (propositional statement == false)")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_for_boolean_results(
        bool model,
        string expectedReason)
    {
        // Arrange

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_when_multiple_assertion_possible_for_policy_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_single_parameter_callback_when_multiple_assertion_possible_for_boolean_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue(_ => "true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_for_policy_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 3).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_when_multiple_assertion_possible_for_boolean_results(
        bool model,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrue((_, _) => "true assertion")
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_policy_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build((bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(true, "propositional statement == true")]
    [InlineData(false, "propositional statement == false")]
    public void Should_use_the_propositional_statement_in_the_reason_when_true_assertion_uses_a_two_parameter_callback_that_returns_a_collection_for_boolean_results(
        bool model,
        string expectedReasonStatement)
    {
        // Arrange
        var expectedReason = string.Join(" & ", Enumerable.Repeat(expectedReasonStatement, 4).Select(s => s.EndsWith(" == false") || s.EndsWith(" == true") ? $"({s})" : s));

        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var withFalseAsScalar =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse("false assertion")
                .Create("propositional statement");

        var withFalseAsParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse(_ => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallback =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalse((_, _) => "false assertion")
                .Create("propositional statement");

        var withFalseAsTwoParameterCallbackThatReturnsACollection =
            Spec.Build(BooleanResultBase<string> (bool b) => underlying.Evaluate(b))
                .WhenTrueYield((_, _) => ["true assertion"])
                .WhenFalseYield((_, _) => ["false assertion"])
                .Create("propositional statement");

        var spec = withFalseAsScalar &
                   withFalseAsParameterCallback &
                   withFalseAsTwoParameterCallback &
                   withFalseAsTwoParameterCallbackThatReturnsACollection;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Fact]
    public void Should_describe_a_policy_result_spec()
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build((bool model) => underlying.Evaluate(model))
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create("is model true");

        // Act
        var act = spec.Description.Statement;

        // Assert
        act.ShouldBe("is model true");
    }

    [Fact]
    public void Should_describe_a_boolean_result_spec()
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is underlying true");

        var spec =
            Spec.Build(BooleanResultBase<string> (bool model) => underlying.Evaluate(model))
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create("is model true");

        // Act
        var act = spec.Description.Statement;

        // Assert
        act.ShouldBe("is model true");
    }
}
