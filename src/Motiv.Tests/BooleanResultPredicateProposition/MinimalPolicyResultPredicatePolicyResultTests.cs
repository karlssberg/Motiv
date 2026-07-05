namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises the property surface of
/// <c>MinimalPolicyResultPredicatePolicyResult&lt;TModel, TMetadata&gt;</c>, produced by a minimal
/// (name-only) proposition whose predicate returns a <see cref="PolicyResultBase{TMetadata}"/>.
/// </summary>
public class MinimalPolicyResultPredicatePolicyResultTests
{
    private static PolicyResultBase<int> IntUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .WhenTrue(1)
        .WhenFalse(0)
        .Create("underlying")
        .Evaluate(model);

    private static PolicyResultBase<string> StringUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .Create("underlying")
        .Evaluate(model);

    // Value is a non-string (int) metadata object, forcing the Assertion switch fallback arm
    // (specDescription.ToReason) rather than the string-passthrough arm.
    [Theory]
    [InlineData(true, 1, "is accepted == true")]
    [InlineData(false, 0, "is accepted == false")]
    public void Non_string_metadata_surfaces_value_and_statement_derived_assertion(
        bool model,
        int expectedValue,
        string expectedReason)
    {
        var underlying = IntUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .Create("is accepted")
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.Value.ShouldBe(expectedValue);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Explanation.Assertions.ShouldBe([expectedReason]);
        result.Description.Reason.ShouldBe(expectedReason);
    }

    // A string metadata object exercises the string-passthrough arm of the Assertion switch:
    // the underlying policy's own assertion text is surfaced verbatim.
    [Theory]
    [InlineData(true, "underlying == true")]
    [InlineData(false, "underlying == false")]
    public void String_metadata_surfaces_underlying_assertion_text(bool model, string expectedAssertion)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .Create("is accepted")
            .Evaluate(model);

        result.Value.ShouldBe(expectedAssertion);
        result.Explanation.Assertions.ShouldBe([expectedAssertion]);
    }
}
