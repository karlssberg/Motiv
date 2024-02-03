using Karlssberg.Motiv.SpecBuilder.Factories;
using Karlssberg.Motiv.SpecBuilder.YieldWhenFalse;
using Karlssberg.Motiv.SpecBuilder.YieldWhenTrue;

namespace Karlssberg.Motiv.SpecBuilder;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
internal class SpecBuilder<TModel> :
    IYieldReasonOrMetadataWhenTrue<TModel>,
    IYieldReasonWhenFalse<TModel>,
    IYieldMetadataWhenFalse<TModel, string>,
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>,
    ISpecFactory<TModel>
{
    private readonly MetadataBuilder<TModel, string, SpecBuilder<TModel>> _because;

    private readonly Func<TModel, bool> _predicate;
    /// <summary>Represents a builder for creating specifications based on a predicate.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    internal SpecBuilder(Func<TModel, bool> predicate)
    {
        _predicate = predicate;
        _because = new MetadataBuilder<TModel, string, SpecBuilder<TModel>>(this);
    }

    public SpecBase<TModel, string> CreateSpec() =>
        new Spec<TModel, string>(
            _because.CandidateDescription!,
            _predicate,
            _because.WhenTrue ?? throw new InvalidOperationException("Must specify a true metadata"),
            _because.WhenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    /// <summary>Sets the description for the specification.</summary>
    /// <param name="description">The description of the specification.</param>
    /// <returns>A new instance of the specification with the specified description.</returns>
    public SpecBase<TModel, string> CreateSpec(string description) =>
        new Spec<TModel, string>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            _predicate,
            _because.WhenTrue ?? throw new InvalidOperationException("Must specify a true metadata"),
            _because.WhenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    IDescriptiveSpecFactory<TModel, string> IYieldMetadataWhenFalse<TModel, string>.YieldWhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    IDescriptiveSpecFactory<TModel, string> IYieldMetadataWhenFalse<TModel, string>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IDescriptiveSpecFactory<TModel, string> IYieldMetadataWhenFalse<TModel, string>.YieldWhenFalse(Func<string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    public ISpecFactory<TModel> YieldWhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    public ISpecFactory<TModel> YieldWhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    ISpecFactory<TModel> IYieldReasonWhenFalse<TModel>.YieldWhenFalse(Func<string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    public IDescriptiveSpecFactory<TModel, string> YieldWhenFalse(Func<string> falseBecause) =>
        new MetadataSpecBuilder<TModel, string>(_predicate, _because.WhenTrue!).YieldWhenFalse(falseBecause);

    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.YieldWhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new MetadataSpecBuilder<TModel, TMetadata>(_predicate, _ => whenTrue);
    }

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new MetadataSpecBuilder<TModel, TMetadata>(_predicate, whenTrue.ThrowIfNull(nameof(whenTrue)));

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new MetadataSpecBuilder<TModel, TMetadata>(_predicate, _ => whenTrue());
    }

    public IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue(Func<TModel, string> trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNull(nameof(trueBecause)));

    public IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue(Func<string> trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNull(nameof(trueBecause)));

    public IYieldReasonWhenFalse<TModel> YieldWhenTrue(string trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));
}