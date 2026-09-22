using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.Tests.BooleanPredicateProposition;

public class MetadataPropositionPolicyResultTests
{
    private static MetadataPropositionPolicyResult<string, string> Create(
        bool satisfied,
        string model,
        Func<string, string> metadataResolver) =>
        new(satisfied, model, metadataResolver, new SpecDescription("is thing"));

    [Theory, AutoData]
    public void Satisfied_ReflectsConstructorArgument(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory, AutoData]
    public void Value_ReturnsResolvedMetadata(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.Value.ShouldBe(value);
    }

    [Theory, AutoData]
    public void MetadataTier_ContainsResolvedValue(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.MetadataTier.Metadata.ShouldBe([value]);
    }

    [Theory, AutoData]
    public void Resolver_IsNotInvokedBeforeValueIsRead(bool satisfied, string model, string value)
    {
        var invocations = 0;
        _ = Create(satisfied, model, _ => { invocations++; return value; });

        invocations.ShouldBe(0);
    }

    [Theory, AutoData]
    public void Resolver_IsInvokedOnceAcrossRepeatedReads(bool satisfied, string model, string value)
    {
        var invocations = 0;
        var result = Create(satisfied, model, _ => { invocations++; return value; });

        _ = result.Value;
        _ = result.Value;
        _ = result.MetadataTier;

        invocations.ShouldBe(1);
    }

    [Theory, AutoData]
    public void Underlying_AlwaysReturnsEmptyCollection(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.Underlying.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void UnderlyingWithValues_AlwaysReturnsEmptyCollection(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void Causes_AlwaysReturnsEmptyCollection(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.Causes.ShouldBeEmpty();
    }

    [Theory, AutoData]
    public void CausesWithValues_AlwaysReturnsEmptyCollection(bool satisfied, string model, string value)
    {
        var result = Create(satisfied, model, _ => value);

        result.CausesWithValues.ShouldBeEmpty();
    }
}
