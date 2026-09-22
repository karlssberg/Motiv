namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises the property surface of
/// <c>MinimalBooleanResultPredicateBooleanResult&lt;TModel, TMetadata&gt;</c>, produced by a minimal
/// (name-only) proposition whose predicate is typed to return a <see cref="BooleanResultBase{TMetadata}"/>.
/// </summary>
public class MinimalBooleanResultPredicateBooleanResultTests
{
    private static BooleanResultBase<int> IntUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .WhenTrue(1)
        .WhenFalse(0)
        .Create("underlying")
        .Evaluate(model);

    // Non-string metadata forces the ResolvedAssertions fallback arm (statement-derived reason)
    // rather than the IEnumerable<string> passthrough arm.
    [Theory]
    [InlineData(true, 1, "is accepted == true")]
    [InlineData(false, 0, "is accepted == false")]
    public void Non_string_metadata_surfaces_values_and_statement_derived_assertions(
        bool model,
        int expectedValue,
        string expectedReason)
    {
        var underlying = IntUnderlying(model);

        var result = Spec
            .Build(BooleanResultBase<int> (bool _) => underlying)
            .Create("is accepted")
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Explanation.Assertions.ShouldBe([expectedReason]);
        result.Description.Reason.ShouldBe(expectedReason);
    }
}
