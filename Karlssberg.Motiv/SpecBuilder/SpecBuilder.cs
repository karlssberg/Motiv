using Karlssberg.Motiv.SpecBuilder.Phase1;
using Karlssberg.Motiv.SpecBuilder.Phase2;
using Karlssberg.Motiv.SpecBuilder.Phase3;

namespace Karlssberg.Motiv.SpecBuilder;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
internal class SpecBuilder<TModel> :
    IRequireTrueReasonOrMetadata<TModel>,
    IRequireFalseReason<TModel>,
    IRequireFalseMetadata<TModel, string>,
    IRequireFalseReasonWhenDescriptionUnresolved<TModel>,
    IRequireActivationWithOrWithoutDescription<TModel>
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

    IRequireActivationWithDescription<TModel, string> IRequireFalseMetadata<TModel, string>.YieldWhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    IRequireActivationWithDescription<TModel, string> IRequireFalseMetadata<TModel, string>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IRequireActivationWithDescription<TModel, string> IRequireFalseMetadata<TModel, string>.YieldWhenFalse(Func<string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    public IRequireActivationWithOrWithoutDescription<TModel> YieldWhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    public IRequireActivationWithOrWithoutDescription<TModel> YieldWhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IRequireActivationWithOrWithoutDescription<TModel> IRequireFalseReason<TModel>.YieldWhenFalse(Func<string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    IRequireActivationWithDescription<TModel, string> IRequireFalseReasonWhenDescriptionUnresolved<TModel>.YieldWhenFalse(Func<TModel, string> falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNull(nameof(falseBecause)));

    public IRequireActivationWithDescription<TModel, string> YieldWhenFalse(Func<string> falseBecause) =>
        new SpecBuilder<TModel, string>(_predicate, _because.WhenTrue!).YieldWhenFalse(falseBecause);

    IRequireActivationWithDescription<TModel, string> IRequireFalseReasonWhenDescriptionUnresolved<TModel>.YieldWhenFalse(string falseBecause) =>
        _because.SetFalseMetadata(falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause)));

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new SpecBuilder<TModel, TMetadata>(_predicate, _ => whenTrue);
    }

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new SpecBuilder<TModel, TMetadata>(_predicate, whenTrue.ThrowIfNull(nameof(whenTrue)));

    public IRequireFalseMetadata<TModel, TMetadata> YieldWhenTrue<TMetadata>(Func<TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new SpecBuilder<TModel, TMetadata>(_predicate, _ => whenTrue());
    }

    public IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue(Func<TModel, string> trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNull(nameof(trueBecause)));

    public IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue(Func<string> trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNull(nameof(trueBecause)));

    public IRequireFalseReason<TModel> YieldWhenTrue(string trueBecause) =>
        _because.SetTrueMetadata(trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));
}

internal class SpecBuilder<TModel, TMetadata> :
    IRequireFalseMetadata<TModel, TMetadata>,
    IRequireActivationWithDescription<TModel, TMetadata>
{
    private readonly MetadataBuilder<TModel, TMetadata, SpecBuilder<TModel, TMetadata>> _metadataBuilder;
    private readonly Func<TModel, bool> _predicate;

    internal SpecBuilder(Func<TModel, bool> predicate, Func<TModel, TMetadata> whenTrue)
    {
        _predicate = predicate;
        _metadataBuilder = new MetadataBuilder<TModel, TMetadata, SpecBuilder<TModel, TMetadata>>(this);
        _metadataBuilder.SetTrueMetadata(whenTrue);
    }

    public SpecBase<TModel, TMetadata> CreateSpec(string description) => new Spec<TModel, TMetadata>(
        description.ThrowIfNullOrWhitespace(nameof(description)),
        _predicate,
        _metadataBuilder.WhenTrue ?? throw new InvalidOperationException("Must specify a true metadata"),
        _metadataBuilder.WhenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse) =>
        _metadataBuilder.SetFalseMetadata(whenFalse.ThrowIfNull(nameof(whenFalse)));

    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse) =>
        _metadataBuilder.SetFalseMetadata(whenFalse.ThrowIfNull(nameof(whenFalse)));

    public IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse) =>
        _metadataBuilder.SetFalseMetadata(whenFalse.ThrowIfNull(nameof(whenFalse)));
}