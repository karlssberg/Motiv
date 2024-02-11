using Karlssberg.Motiv.ChangeMetadataType.WhenFalse;

namespace Karlssberg.Motiv.ChangeMetadataType;

public readonly struct ChangeMetadataBuilder<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec,
    Func<TModel, string> trueBecause,
    string? candidateDescription = null)
    : IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata>, IYieldReasonWhenFalse<TModel, TMetadata>
{
    public SpecBase<TModel, string> WhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec(_ => falseBecause);
    }

    public SpecBase<TModel, string> WhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec(falseBecause);
    }

    public SpecBase<TModel, string> WhenFalse(Func<string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec(_ => falseBecause());
    }

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata>.WhenFalse(Func<TModel, string> falseBecause) =>
        WhenFalse(falseBecause);

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata>.WhenFalse(string falseBecause) =>
        WhenFalse(falseBecause);
    
    private SpecBase<TModel, string> CreateSpec(Func<TModel, string> falseBecause) =>
        new ChangeMetadataSpec<TModel, string, TMetadata>(
            spec,
            trueBecause,
            falseBecause,
            candidateDescription);

    SpecBase<TModel, TMetadata> IChangeReasonBuilder<TModel, TMetadata>.Spec => spec;

    Func<TModel, string> IChangeReasonBuilder<TModel, TMetadata>.TrueBecause => trueBecause;
}

