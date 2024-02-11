using Karlssberg.Motiv.Proposition.Factories;
using Karlssberg.Motiv.Proposition.YieldWhenFalse;
using Karlssberg.Motiv.Proposition.YieldWhenTrue;

namespace Karlssberg.Motiv.Proposition;

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

    IDescriptiveSpecFactory<TModel, string> IYieldMetadataWhenFalse<TModel, string>.WhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    IDescriptiveSpecFactory<TModel, string> IYieldMetadataWhenFalse<TModel, string>.WhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    public ISpecFactory<TModel> WhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    public ISpecFactory<TModel> WhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.WhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IDescriptiveSpecFactory<TModel, string> IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel>.WhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    public IYieldMetadataWhenFalse<TModel, TMetadata> WhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new MetadataSpecBuilder<TModel, TMetadata>(_predicate, _ => whenTrue);
    }

    public IYieldMetadataWhenFalse<TModel, TMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new MetadataSpecBuilder<TModel, TMetadata>(_predicate, whenTrue.ThrowIfNull(nameof(whenTrue)));

    public IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> WhenTrue(Func<TModel, string> trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNull(nameof(trueBecause)));

    public IYieldReasonWhenFalse<TModel> WhenTrue(string trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));
}