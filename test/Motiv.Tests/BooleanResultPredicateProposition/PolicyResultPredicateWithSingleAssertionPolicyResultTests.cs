namespace Motiv.Tests.BooleanResultPredicateProposition;

/// <summary>
/// Exercises <c>PolicyResultPredicateWithSingleAssertionPolicyResult&lt;TModel, TUnderlyingMetadata&gt;</c>,
/// produced by an unnamed single-assertion explanation proposition (<c>WhenTrue(string) + WhenFalse(string)</c>)
/// whose predicate returns a <see cref="PolicyResultBase{TMetadata}"/>. The underlying metadata type is varied
/// to cover both arms of the value-bearing collection casts.
/// </summary>
public class PolicyResultPredicateWithSingleAssertionPolicyResultTests
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

    [Theory]
    [InlineData(true, "t")]
    [InlineData(false, "f")]
    public void String_underlying_surfaces_value_bearing_underlying(bool model, string expected)
    {
        var underlying = StringUnderlying(model);

        var result = Spec
            .Build((bool _) => underlying)
            .WhenTrue("t")
            .WhenFalse("f")
            .Create()
            .Evaluate(model);

        result.Satisfied.ShouldBe(model);
        result.Value.ShouldBe(expected);
        result.Assertions.ShouldBe([expected]);
        result.Description.Reason.ShouldBe(expected);
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
            .Build((bool _) => underlying)
            .WhenTrue("t")
            .WhenFalse("f")
            .Create()
            .Evaluate(model);

        result.Underlying.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.Causes.ShouldHaveSingleItem().ShouldBeSameAs(underlying);
        result.UnderlyingWithValues.ShouldBeEmpty();
        result.CausesWithValues.ShouldBeEmpty();
    }
}
