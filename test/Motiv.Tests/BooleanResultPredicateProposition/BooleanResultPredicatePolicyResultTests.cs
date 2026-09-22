namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises <c>BooleanResultPredicatePolicyResult&lt;TModel, TMetadata, TUnderlyingMetadata&gt;</c>, produced by a
/// single-value metadata proposition (<c>WhenTrue(obj) + WhenFalse(obj) + Create(name)</c>) whose predicate is typed
/// to return a <see cref="BooleanResultBase{TMetadata}"/>. The underlying metadata type is varied relative to the
/// replacement metadata type to cover both arms of the value-bearing collection casts.
/// </summary>
public class BooleanResultPredicatePolicyResultTests
{
    private static BooleanResultBase<int> IntUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .WhenTrue(1)
        .WhenFalse(0)
        .Create("underlying")
        .Evaluate(model);

    private static BooleanResultBase<string> StringUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .Create("underlying")
        .Evaluate(model);

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
            .Build(BooleanResultBase<int> (bool _) => underlying)
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

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, -200)]
    public void Differing_metadata_types_fall_back_to_empty_value_bearing_collections(bool model, int expectedValue)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build(BooleanResultBase<string> (bool _) => underlying)
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
