namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises <c>BooleanResultPredicateWithSingleAssertionPolicyResult&lt;TModel, TUnderlyingMetadata&gt;</c>,
/// produced by an unnamed single-assertion explanation proposition (<c>WhenTrue(string) + WhenFalse(string)</c>)
/// whose predicate is typed to return a <see cref="BooleanResultBase{TMetadata}"/>. The underlying metadata type
/// is varied to cover both arms of the value-bearing collection casts.
/// </summary>
public class BooleanResultPredicateWithSingleAssertionPolicyResultTests
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
    [InlineData(false, "f")]
    public void String_underlying_surfaces_value_bearing_underlying(bool model, string expected)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build(BooleanResultBase<string> (bool _) => underlying)
            .WhenTrue("t")
            .WhenFalse("f")
            .Create()
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.Value.ShouldBe(expected);
        result.UnderlyingWithValues.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
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
            .WhenFalse("f")
            .Create()
            .Evaluate(model);

        result.UnderlyingWithValues.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
    }
}
