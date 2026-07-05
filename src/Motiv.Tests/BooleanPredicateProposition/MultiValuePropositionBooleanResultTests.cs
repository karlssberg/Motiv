using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.Tests.BooleanPredicateProposition;

public class MultiValuePropositionBooleanResultTests
{
    private static MultiValuePropositionBooleanResult<string, string> Create(
        bool satisfied,
        string model,
        Func<string, IEnumerable<string>> metadataResolver) =>
        new(satisfied, model, metadataResolver, new SpecDescription("is thing"));

    [Theory, AutoData]
    public void Satisfied_ReflectsConstructorArgument(bool satisfied, string model, string[] values)
    {
        var result = Create(satisfied, model, _ => values);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory, AutoData]
    public void MetadataTier_ContainsResolvedValues(bool satisfied, string model, string[] values)
    {
        var result = Create(satisfied, model, _ => values);

        result.MetadataTier.Metadata.ShouldBe(values, ignoreOrder: true);
    }

    [Theory, AutoData]
    public void Resolver_IsNotInvokedBeforeMetadataTierIsRead(bool satisfied, string model, string[] values)
    {
        var invocations = 0;
        _ = Create(satisfied, model, _ => { invocations++; return values; });

        invocations.ShouldBe(0);
    }

    [Theory, AutoData]
    public void Resolver_IsInvokedOnceAcrossRepeatedReads(bool satisfied, string model, string[] values)
    {
        var invocations = 0;
        var result = Create(satisfied, model, _ => { invocations++; return values; });

        _ = result.MetadataTier;
        _ = result.MetadataTier;

        invocations.ShouldBe(1);
    }

    [Theory, AutoData]
    public void Underlying_AlwaysReturnsEmptyCollection(bool satisfied, string model, string[] values)
    {
        var result = Create(satisfied, model, _ => values);

        result.Underlying.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_AlwaysReturnsEmptyCollection(bool satisfied, string model, string[] values)
    {
        var result = Create(satisfied, model, _ => values);

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void Causes_AlwaysReturnsEmptyCollection(bool satisfied, string model, string[] values)
    {
        var result = Create(satisfied, model, _ => values);

        result.Causes.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void CausesWithValues_AlwaysReturnsEmptyCollection(bool satisfied, string model, string[] values)
    {
        var result = Create(satisfied, model, _ => values);

        result.CausesWithValues.ShouldBeEmpty();
    }
}
