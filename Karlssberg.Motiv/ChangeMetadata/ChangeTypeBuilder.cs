using Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue) :
    IYieldMetadataWhenFalse<TModel, TMetadata>,
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>
{
    private Func<TModel, TMetadata>? _whenFalse;

    public SpecBase<TModel, TMetadata> CreateSpec() =>
        new ChangeMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            whenTrue,
            _whenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    public SpecBase<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return CreateSpec();
    }

    public SpecBase<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        _whenFalse = whenFalse;
        return CreateSpec();
    }

    public SpecBase<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse)
    {
        _whenFalse = _ => whenFalse();
        return CreateSpec();
    }

    public SpecBase<TModel, string> YieldWhenFalse(string falseBecause) =>
        new ChangeTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => falseBecause)
            .CreateSpec();

    public SpecBase<TModel, string> YieldWhenFalse(Func<TModel, string> falseBecause) =>
        new ChangeTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, falseBecause)
            .CreateSpec();

    public SpecBase<TModel, string> YieldWhenFalse(Func<string> falseBecause) =>
        new ChangeTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => falseBecause())
            .CreateSpec();
}