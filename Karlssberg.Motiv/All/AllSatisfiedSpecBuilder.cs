using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.IgnoreUnderlyingMetadata;

namespace Karlssberg.Motiv.All;

internal class AllSatisfiedSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : HigherOrderMetadataSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata>
{
    public override IHigherOrderSpecFactory<TModel, TAltMetadata> Yield<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AllSatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());

    public override IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAllTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata) => 
        new AllSatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());

    public override IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AllSatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());
    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec() =>
        new AllSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec, YieldMetadata);

    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description) =>
        new AllSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec, YieldMetadata, description);
}