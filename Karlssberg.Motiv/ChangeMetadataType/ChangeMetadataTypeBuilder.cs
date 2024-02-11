using Karlssberg.Motiv.ChangeMetadataType.WhenFalse;

namespace Karlssberg.Motiv.ChangeMetadataType;

internal struct ChangeMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue)
    : IYieldMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    public SpecBase<TModel, TUnderlyingMetadata> Spec => spec;
    public Func<TModel, TMetadata> WhenTrue => whenTrue;
    private Func<TModel, TMetadata>? _whenFalse;

    public SpecBase<TModel, TMetadata> WhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return CreateSpec();
    }

    public SpecBase<TModel, TMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        _whenFalse = whenFalse;
        return CreateSpec();
    }

    private SpecBase<TModel, TMetadata> CreateSpec() =>
        new ChangeMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            WhenTrue,
            _whenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));
}