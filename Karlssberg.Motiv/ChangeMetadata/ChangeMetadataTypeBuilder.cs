using Karlssberg.Motiv.SpecBuilder.Phase2;
using Karlssberg.Motiv.SpecBuilder.Phase3;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue) :
    IRequireFalseMetadata<TModel, TMetadata>,
    IRequireActivationWithDescription<TModel, TMetadata>,
    IRequireFalseReasonWhenDescriptionUnresolved<TModel>
{
    private Func<TModel, TMetadata>? _whenFalse;

    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new ChangeMetadataTypeSpec<TModel, TMetadata, TUnderlyingMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            spec,
            whenTrue,
            _whenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse)
    {
        _whenFalse = _ => whenFalse;
        return this;
    }

    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        _whenFalse = whenFalse;
        return this;
    }

    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse)
    {
        _whenFalse = _ => whenFalse();
        return this;
    }

    public IRequireActivationWithDescription<TModel, string> YieldWhenFalse(string falseBecause) =>
        new ChangeMetadataTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => falseBecause);

    public IRequireActivationWithDescription<TModel, string> YieldWhenFalse(Func<TModel, string> falseBecause) =>
        new ChangeMetadataTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, falseBecause);

    public IRequireActivationWithDescription<TModel, string> YieldWhenFalse(Func<string> falseBecause) =>
        new ChangeMetadataTypeBuilder<TModel, string, TUnderlyingMetadata>(spec, _ => falseBecause());
}