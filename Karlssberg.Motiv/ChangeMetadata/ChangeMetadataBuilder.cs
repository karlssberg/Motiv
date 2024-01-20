using Karlssberg.Motiv.SpecBuilder.Phase1;
using Karlssberg.Motiv.SpecBuilder.Phase2;
using Karlssberg.Motiv.SpecBuilder.Phase3;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeMetadataBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec) :
    IRequireTrueReasonOrMetadata<TModel>,
    IRequireFalseReasonWhenDescriptionUnresolved<TModel>,
    IRequireFalseReason<TModel>,
    IRequireActivationWithOrWithoutDescription<TModel>
{
    private string? _candidateDescription;
    private Func<TModel, string>? _falseBecause;
    private Func<TModel, string>? _trueBecause;
    public SpecBase<TModel, string> CreateSpec(string description) =>
        new ChangeMetadataSpec<TModel, string, TUnderlyingMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            spec,
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"));
    public SpecBase<TModel, string> CreateSpec() =>
        new ChangeMetadataSpec<TModel, string, TUnderlyingMetadata>(
            _candidateDescription ?? spec.Description,
            spec,
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"));

    public IRequireActivationWithOrWithoutDescription<TModel> YieldWhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause;
        return this;
    }
    public IRequireActivationWithOrWithoutDescription<TModel> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));
        return this;
    }

    public IRequireActivationWithOrWithoutDescription<TModel> YieldWhenFalse(Func<string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause();
        return this;
    }
    IRequireActivationWithDescription<TModel, string> IRequireFalseReasonWhenDescriptionUnresolved<TModel>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    IRequireActivationWithDescription<TModel, string> IRequireFalseReasonWhenDescriptionUnresolved<TModel>.YieldWhenFalse(Func<string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    IRequireActivationWithDescription<TModel, string> IRequireFalseReasonWhenDescriptionUnresolved<TModel>.YieldWhenFalse(string falseBecause) =>
        YieldWhenFalse(falseBecause);

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(TMetadata whenTrue) =>
        new ChangeMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, _ => whenTrue);

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new ChangeMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, whenTrue);

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TMetadata> whenTrue) =>
        new ChangeMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, _ => whenTrue());

    public IRequireFalseReason<TModel> YieldWhenTrue(string trueBecause)
    {
        _candidateDescription = trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        trueBecause.ThrowIfNull(nameof(trueBecause));
        _trueBecause = _ => trueBecause;
        return this;
    }
    public IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue(Func<TModel, string> trueBecause)
    {
        _trueBecause = trueBecause.ThrowIfNull(nameof(trueBecause));
        return this;
    }

    public IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue(Func<string> trueBecause)
    {
        trueBecause.ThrowIfNull(nameof(trueBecause));
        _trueBecause = _ => trueBecause();
        return this;
    }
}