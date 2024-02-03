using Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

namespace Karlssberg.Motiv.ChangeMetadata;

public class ChangeMetadataBuilder<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec) :
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>,
    IYieldReasonWhenFalse<TModel>
{
    private string? _candidateDescription;
    private Func<TModel, string>? _falseBecause;
    private Func<TModel, string>? _trueBecause;

    public IYieldMetadataWhenFalse<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(TAltMetadata whenTrue) =>
        new ChangeMetadataTypeBuilder<TModel, TAltMetadata, TMetadata>(spec, _ => whenTrue);

    public IYieldMetadataWhenFalse<TModel, TAltMetadata> YieldWhenTrue<TAltMetadata>(Func<TModel, TAltMetadata> whenTrue) =>
        new ChangeMetadataTypeBuilder<TModel, TAltMetadata, TMetadata>(spec, whenTrue);


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

    public SpecBase<TModel, string> YieldWhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause;
        return CreateSpec();
    }

    public SpecBase<TModel, string> YieldWhenFalse(Func<TModel, string> falseBecause)
    {
        _falseBecause = falseBecause.ThrowIfNull(nameof(falseBecause));
        return CreateSpec();
    }

    public SpecBase<TModel, string> YieldWhenFalse(Func<string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        _falseBecause = _ => falseBecause();
        return CreateSpec();
    }

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(
        Func<TModel, string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(
        Func<string> falseBecause) =>
        YieldWhenFalse(falseBecause);

    SpecBase<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.
        YieldWhenFalse(string falseBecause) =>
        YieldWhenFalse(falseBecause);

    private SpecBase<TModel, string> CreateSpec() =>
        new ChangeMetadataSpec<TModel, string, TMetadata>(
            spec,
            _trueBecause ?? throw new InvalidOperationException("Must specify a true metadata"),
            _falseBecause ?? throw new InvalidOperationException("Must specify a false metadata"),
            _candidateDescription);
}