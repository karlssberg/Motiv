using Karlssberg.Motiv.SpecBuilder.Phase1;
using Karlssberg.Motiv.SpecBuilder.Phase2;
using Karlssberg.Motiv.SpecBuilder.Phase3;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeMetadataBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec) :
    IYieldReasonOrMetadataWhenTrue<TModel>,
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>,
    IYieldReasonWhenFalse<TModel>,
    ISpecFactory<TModel>
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

    public ISpecFactory<TModel> YieldWhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause;
        return this;
    }
    public ISpecFactory<TModel> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));
        return this;
    }

    public ISpecFactory<TModel> YieldWhenFalse(Func<string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause();
        return this;
    }
    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(Func<string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(string falseBecause) =>
        YieldWhenFalse(falseBecause);

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue<TMetadata>(TMetadata whenTrue) =>
        new ChangeTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, _ => whenTrue);

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new ChangeTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, whenTrue);

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TMetadata> whenTrue) =>
        new ChangeTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec, _ => whenTrue());

    public IYieldReasonWhenFalse<TModel> YieldWhenTrue(string trueBecause)
    {
        _candidateDescription = trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        trueBecause.ThrowIfNull(nameof(trueBecause));
        _trueBecause = _ => trueBecause;
        return this;
    }
    public IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue(Func<TModel, string> trueBecause)
    {
        _trueBecause = trueBecause.ThrowIfNull(nameof(trueBecause));
        return this;
    }

    public IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue(Func<string> trueBecause)
    {
        trueBecause.ThrowIfNull(nameof(trueBecause));
        _trueBecause = _ => trueBecause();
        return this;
    }
}