namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises <c>PolicyResultPredicateMultiAssertionExplanationBooleanResult&lt;TModel, TUnderlyingMetadata&gt;</c>,
/// produced by an unnamed explanation proposition (<c>WhenTrue(string) + WhenFalseYield</c>) whose predicate
/// returns a <see cref="PolicyResultBase{TMetadata}"/>. The underlying metadata type is varied to cover both
/// the compatible and incompatible arms of the <c>as IEnumerable&lt;BooleanResultBase&lt;string&gt;&gt; ?? []</c> cast.
/// </summary>
public class PolicyResultPredicateMultiAssertionExplanationBooleanResultTests
{
    private static PolicyResultBase<string> StringUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .Create("underlying")
        .Evaluate(model);

    private static PolicyResultBase<int> IntUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .WhenTrue(1)
        .WhenFalse(0)
        .Create("underlying")
        .Evaluate(model);

    // Underlying metadata is string: the array cast to IEnumerable<BooleanResultBase<string>> succeeds,
    // so the value-bearing collections surface the underlying result.
    [Theory]
    [InlineData(true, "t == true", "t")]
    [InlineData(false, "t == false", "f1", "f2")]
    public void String_underlying_surfaces_value_bearing_underlying(
        bool model,
        string expectedReason,
        params string[] expectedAssertions)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .WhenTrue("t")
            .WhenFalseYield((_, _) => ["f1", "f2"])
            .Create()
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.MetadataTier.Metadata.ShouldBe(expectedAssertions, ignoreOrder: true);
        result.Assertions.ShouldBe(expectedAssertions);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Description.Reason.ShouldBe(expectedReason);
    }

    // Underlying metadata is int: the cast to IEnumerable<BooleanResultBase<string>> fails, so the
    // value-bearing collections fall back to empty while the type-erased collections still surface it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Int_underlying_falls_back_to_empty_value_bearing_collections(bool model)
    {
        var underlying = IntUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .WhenTrue("t")
            .WhenFalseYield((_, _) => ["f1", "f2"])
            .Create()
            .Evaluate(model);

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
        result.MetadataTier.Metadata.ShouldBe(model ? ["t"] : ["f1", "f2"], ignoreOrder: true);
    }
}
