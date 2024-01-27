using Karlssberg.Motiv.SpecBuilder.Phase2;
using Karlssberg.Motiv.SpecBuilder.Phase3;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue) :
    IYieldMetadataWhenFalse<TModel, TMetadata>,
    IDescriptiveSpecFactory<TModel, TMetadata>,
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>
{
    private Func<TModel, TMetadata>? _whenFalse;

    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new ChangeMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            spec,
            whenTrue,
            _whenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return this;
    }

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        _whenFalse = whenFalse;
        return this;
    }

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse)
    {
        _whenFalse = _ => whenFalse();
        return this;
    }

    public IDescriptiveSpecFactory<TModel, string> YieldWhenFalse(string falseBecause) =>
        new ChangeTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => falseBecause);

    public IDescriptiveSpecFactory<TModel, string> YieldWhenFalse(Func<TModel, string> falseBecause) =>
        new ChangeTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, falseBecause);

    public IDescriptiveSpecFactory<TModel, string> YieldWhenFalse(Func<string> falseBecause) =>
        new ChangeTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => falseBecause());
}