using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.IgnoreUnderlyingMetadata;
using Karlssberg.Motiv.NSatisfied;

namespace Karlssberg.Motiv.All;

internal class AllSatisfiedSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : HigherOrderSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata>
{
    /// <inheritdoc />
    public override IHigherOrderSpecFactory<TModel, TAltMetadata> Yield<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>>
            metadata) =>
        new AllSatisfiedSpecBuilder<TModel, TAltMetadata, TUnderlyingMetadata>(underlyingSpec)
            .Yield(metadata);
    
    /// <inheritdoc />
    public override IYieldMetadataWhenFalse<TModel, TAltMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata) => 
        new AllSatisfiedSpecBuilder<TModel, TAltMetadata, TUnderlyingMetadata>(underlyingSpec)
            .YieldWhenAllTrue(metadata);

    /// <inheritdoc />
    public override IYieldMetadataWhenFalse<TModel, TAltMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AllSatisfiedSpecBuilder<TModel, TAltMetadata, TUnderlyingMetadata>(underlyingSpec)
            .YieldWhenAnyTrue(metadata);
    
    /// <inheritdoc />
    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec() =>
        new AllSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(
            underlyingSpec, 
            new MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>(YieldMetadata));

    /// <inheritdoc />
    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description) =>
        new AllSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(
            underlyingSpec, 
            new MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>(YieldMetadata), description);
}