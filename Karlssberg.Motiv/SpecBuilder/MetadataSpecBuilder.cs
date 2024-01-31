using Karlssberg.Motiv.SpecBuilder.Factories;
using Karlssberg.Motiv.SpecBuilder.YieldWhenFalse;

namespace Karlssberg.Motiv.SpecBuilder;

internal class MetadataSpecBuilder<TModel, TMetadata> :
    IYieldMetadataWhenFalse<TModel, TMetadata>,
    IDescriptiveSpecFactory<TModel, TMetadata>
{
    private readonly MetadataBuilder<TModel, TMetadata, MetadataSpecBuilder<TModel, TMetadata>> _metadataBuilder;
    private readonly Func<TModel, bool> _predicate;

    internal MetadataSpecBuilder(Func<TModel, bool> predicate, Func<TModel, TMetadata> whenTrue)
    {
        _predicate = predicate;
        _metadataBuilder = new MetadataBuilder<TModel, TMetadata, MetadataSpecBuilder<TModel, TMetadata>>(this);
        _metadataBuilder.SetTrueMetadata(whenTrue);
    }

    public SpecBase<TModel, TMetadata> CreateSpec(string description) => new Spec<TModel, TMetadata>(
        description.ThrowIfNullOrWhitespace(nameof(description)),
        _predicate,
        _metadataBuilder.WhenTrue ?? throw new InvalidOperationException("Must specify a true metadata"),
        _metadataBuilder.WhenFalse ?? throw new InvalidOperationException("Must specify a false metadata"));

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse) =>
        _metadataBuilder.SetFalseMetadata(whenFalse.ThrowIfNull(nameof(whenFalse)));

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse) =>
        _metadataBuilder.SetFalseMetadata(whenFalse.ThrowIfNull(nameof(whenFalse)));

    public IDescriptiveSpecFactory<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse) =>
        _metadataBuilder.SetFalseMetadata(whenFalse.ThrowIfNull(nameof(whenFalse)));
}