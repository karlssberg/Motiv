using Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeMetadataBuilder<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec,
    Func<TModel, string> trueBecause,
    string? candidateDescription = null) :
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata>,
    IYieldReasonWhenFalse<TModel, TMetadata>
{
    public SpecBase<TModel, string> YieldWhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec(_ => falseBecause);
    }

    public SpecBase<TModel, string> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec(falseBecause);
    }

    public SpecBase<TModel, string> YieldWhenFalse(Func<string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec(_ => falseBecause());
    }

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata>.YieldWhenFalse(string falseBecause) =>
        YieldWhenFalse(falseBecause);
    
    private SpecBase<TModel, string> CreateSpec(Func<TModel, string> falseBecause) =>
        new ChangeMetadataSpec<TModel, string, TMetadata>(
            spec,
            trueBecause,
            falseBecause,
            candidateDescription);

    SpecBase<TModel, TMetadata> IChangeReasonBuilder<TModel, TMetadata>.Spec => spec;

    Func<TModel, string> IChangeReasonBuilder<TModel, TMetadata>.TrueBecause => trueBecause;
}

