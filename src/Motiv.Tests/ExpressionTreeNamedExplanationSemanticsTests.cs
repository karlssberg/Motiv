namespace Motiv.Tests;

/// <summary>
/// Pins the ExpressionTree family's explanation semantics after the metadata-semantics refactor:
/// named explanation specs surface the underlying decomposed clause assertions (matching
/// <c>ExpressionTreeMetadataProposition</c> behavior), while unnamed explanation specs keep the
/// user-supplied because-strings, falling back to the underlying assertions (or, for the single-string
/// lazies, to <c>description.ToReason(satisfied)</c>) when those strings are degenerate.
/// </summary>
public class ExpressionTreeNamedExplanationSemanticsTests
{
    [Fact]
    public void Should_use_underlying_assertions_when_true_for_named_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create("is positive");

        var act = spec.Evaluate(1);

        act.Assertions.ShouldBe(["n > 0 == true"]);
    }

    [Fact]
    public void Should_use_underlying_assertions_when_false_for_named_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create("is positive");

        var act = spec.Evaluate(-1);

        act.Assertions.ShouldBe(["n > 0 == false"]);
    }

    [Fact]
    public void Should_use_name_suffix_reason_when_true_for_named_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create("is positive");

        spec.Evaluate(1).Reason.ShouldBe("is positive == true");
    }

    [Fact]
    public void Should_use_name_suffix_reason_when_false_for_named_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create("is positive");

        spec.Evaluate(-1).Reason.ShouldBe("is positive == false");
    }

    [Fact]
    public void Should_use_because_strings_as_values_when_true_for_named_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create("is positive");

        spec.Evaluate(1).Values.ShouldBe(["pos"]);
    }

    [Fact]
    public void Should_use_because_strings_as_values_when_false_for_named_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create("is positive");

        spec.Evaluate(-1).Values.ShouldBe(["neg"]);
    }

    [Fact]
    public void Should_keep_because_strings_as_assertions_when_true_for_unnamed_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create();

        spec.Evaluate(1).Assertions.ShouldBe(["pos"]);
    }

    [Fact]
    public void Should_keep_because_strings_as_assertions_when_false_for_unnamed_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse("neg")
            .Create();

        spec.Evaluate(-1).Assertions.ShouldBe(["neg"]);
    }

    [Fact]
    public void Should_use_because_string_when_not_degenerate_for_unnamed_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse((_, _) => " ")
            .Create();

        spec.Evaluate(1).Assertions.ShouldBe(["pos"]);
    }

    [Fact]
    public void Should_fall_back_to_reason_when_because_string_is_degenerate_for_unnamed_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse((_, _) => " ")
            .Create();

        // The false-branch because-string is whitespace-only (degenerate), so the single-string lazy
        // falls back to the expression-derived reason rather than the underlying assertions.
        spec.Evaluate(-1).Assertions.ShouldBe(["n > 0 == false"]);
    }

    [Fact]
    public void Should_keep_raw_because_string_in_values_when_degenerate_for_unnamed_explanation_func_factory()
    {
        // Routes through ExpressionTreeExplanationProposition (both trueBecause/falseBecause are Funcs).
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse((_, _) => " ")
            .Create();

        var result = spec.Evaluate(-1);

        // The Assertions/Reason still fall back to the expression-derived reason...
        result.Assertions.ShouldBe(["n > 0 == false"]);
        // ...but Values must retain the raw (degenerate) because-string, not the fallback.
        result.Values.ShouldBe([" "]);
    }

    [Fact]
    public void Should_keep_raw_because_string_in_values_when_degenerate_for_unnamed_explanation_with_name_factory()
    {
        // Routes through ExpressionTreeWithSingleTrueAssertionProposition (trueBecause is a plain string).
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalse(" ")
            .Create();

        var result = spec.Evaluate(-1);

        // The Assertions/Reason still fall back to the expression-derived reason...
        result.Assertions.ShouldBe(["n > 0 == false"]);
        // ...but Values must retain the raw (degenerate) because-string, not the fallback.
        result.Values.ShouldBe([" "]);
    }

    [Fact]
    public void Should_not_throw_when_creating_with_a_whitespace_whentrue_and_no_explicit_statement()
    {
        // ExplanationWithNameExpressionTreePropositionFactory.Create() derives its statement from the
        // expression itself, not from trueBecause, so no guard is added at construction time - a blank
        // trueBecause is only detected (and degrades gracefully) at evaluation time via ElseFallback.
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue(" ")
            .WhenFalse("neg")
            .Create();

        spec.Evaluate(1).Assertions.ShouldBe(["n > 0 == true"]);
    }

    [Fact]
    public void Should_use_underlying_assertions_when_true_for_named_multi_assertion_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalseYield((_, _) => ["neg1", "neg2"])
            .Create("is positive");

        spec.Evaluate(1).Assertions.ShouldBe(["n > 0 == true"]);
    }

    [Fact]
    public void Should_use_underlying_assertions_when_false_for_named_multi_assertion_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalseYield((_, _) => ["neg1", "neg2"])
            .Create("is positive");

        spec.Evaluate(-1).Assertions.ShouldBe(["n > 0 == false"]);
    }

    [Fact]
    public void Should_keep_because_strings_when_true_for_unnamed_multi_assertion_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalseYield((_, _) => ["neg1", "neg2"])
            .Create();

        spec.Evaluate(1).Assertions.ShouldBe(["pos"]);
    }

    [Fact]
    public void Should_keep_because_strings_when_false_for_unnamed_multi_assertion_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalseYield((_, _) => ["neg1", "neg2"])
            .Create();

        spec.Evaluate(-1).Assertions.ShouldBe(["neg1", "neg2"]);
    }

    [Fact]
    public void Should_fall_back_to_underlying_assertions_when_all_because_strings_are_degenerate_for_unnamed_multi_assertion_explanation()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .WhenTrue("pos")
            .WhenFalseYield((_, _) => [" ", ""])
            .Create();

        spec.Evaluate(-1).Assertions.ShouldBe(["n > 0 == false"]);
    }

    [Fact]
    public void Should_throw_when_creating_an_unnamed_multi_assertion_explanation_with_a_blank_whentrue()
    {
        // Unlike the single-assertion factory, this one's statement IS trueBecause (there is no
        // expression-derived fallback name for the multi-assertion case), so a guard is required.
        var create = () => Spec
            .From((int n) => n > 0)
            .WhenTrue(" ")
            .WhenFalseYield((_, _) => ["neg1", "neg2"])
            .Create();

        Should.Throw<ArgumentException>(create);
    }

    [Fact]
    public void Should_pin_minimal_proposition_assertions_when_true()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .Create("is positive");

        spec.Evaluate(1).Assertions.ShouldBe(["n > 0"]);
    }

    [Fact]
    public void Should_pin_minimal_proposition_assertions_when_false()
    {
        var spec = Spec
            .From((int n) => n > 0)
            .Create("is positive");

        spec.Evaluate(-1).Assertions.ShouldBe(["n <= 0"]);
    }
}
