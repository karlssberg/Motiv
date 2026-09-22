namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises <c>BooleanResultPredicateMultiAssertionExplanationBooleanResult&lt;TModel, TUnderlyingMetadata&gt;</c>,
/// produced by an unnamed explanation proposition (<c>WhenTrue(string) + WhenFalseYield</c>) whose predicate is
/// typed to return a <see cref="BooleanResultBase{TMetadata}"/>. The underlying metadata type is varied to cover
/// both arms of the <c>as IEnumerable&lt;BooleanResultBase&lt;string&gt;&gt; ?? []</c> cast.
/// </summary>
public class BooleanResultPredicateMultiAssertionExplanationBooleanResultTests
{
    private static BooleanResultBase<string> StringUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .Create("underlying")
        .Evaluate(model);

    private static BooleanResultBase<int> IntUnderlying(bool model) => Spec
        .Build((bool b) => b)
        .WhenTrue(1)
        .WhenFalse(0)
        .Create("underlying")
        .Evaluate(model);

    [Theory]
    [InlineData(true, "t")]
    [InlineData(false, "f1", "f2")]
    public void String_underlying_surfaces_value_bearing_underlying(
        bool model,
        params string[] expectedAssertions)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build(BooleanResultBase<string> (bool _) => underlying)
            .WhenTrue("t")
            .WhenFalseYield((_, _) => ["f1", "f2"])
            .Create()
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.Assertions.ShouldBe(expectedAssertions);
        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.CausesWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Int_underlying_falls_back_to_empty_value_bearing_collections(bool model)
    {
        var underlying = IntUnderlying(model);

        var result = Spec
            .Build(BooleanResultBase<int> (bool _) => underlying)
            .WhenTrue("t")
            .WhenFalseYield((_, _) => ["f1", "f2"])
            .Create()
            .Evaluate(model);

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
    }
}
