using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.IgnoreUnderlyingMetadata;

namespace Karlssberg.Motiv.Any;

internal class AnySatisfiedSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : HigherOrderSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata>
{
    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec() =>
        new AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec, YieldMetadata);
    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description) =>
        new AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec, YieldMetadata, description);
    public override IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AnySatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());
    public override IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnything<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AnySatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());
}