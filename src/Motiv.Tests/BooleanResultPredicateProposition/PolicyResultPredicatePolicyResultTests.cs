namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises <c>PolicyResultPredicatePolicyResult&lt;TModel, TMetadata, TUnderlyingMetadata&gt;</c>, produced by a
/// single-value metadata proposition (<c>WhenTrue(obj) + WhenFalse(obj) + Create(name)</c>) whose predicate returns
/// a <see cref="PolicyResultBase{TMetadata}"/>. The underlying metadata type is varied relative to the replacement
/// metadata type to cover both arms of the value-bearing collection casts.
/// </summary>
public class PolicyResultPredicatePolicyResultTests
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

    // Replacement metadata (int) matches the underlying metadata (int): the cast succeeds.
    [Theory]
    [InlineData(true, 200, "is accepted == true")]
    [InlineData(false, -200, "is accepted == false")]
    public void Matching_metadata_types_surface_value_bearing_underlying(
        bool model,
        int expectedValue,
        string expectedReason)
    {
        var underlying = IntUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is accepted")
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.Value.ShouldBe(expectedValue);
        result.MetadataTier.Metadata.ShouldBe([expectedValue]);
        result.Description.Reason.ShouldBe(expectedReason);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    // Replacement metadata (int) differs from the underlying metadata (string): the cast fails.
    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, -200)]
    public void Differing_metadata_types_fall_back_to_empty_value_bearing_collections(bool model, int expectedValue)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is accepted")
            .Evaluate(model);

        result.Value.ShouldBe(expectedValue);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
    }
}
