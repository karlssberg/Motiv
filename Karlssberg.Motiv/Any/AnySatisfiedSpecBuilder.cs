using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.IgnoreUnderlyingMetadata;

namespace Karlssberg.Motiv.Any;

internal class AnySatisfiedSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : HigherOrderMetadataSpecBuilderBase<TModel, TMetadata, TUnderlyingMetadata>
{ 
    public override IHigherOrderSpecFactory<TModel, TAltMetadata> Yield<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AnySatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());
    
    public override IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAllTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AnySatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());

    public override IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata) =>
        new AnySatisfiedSpecBuilder<TModel, TAltMetadata, TMetadata>(
            underlyingSpec.IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>());
    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec() =>
        new AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec, YieldMetadata);

    public override SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description) =>
        new AnySatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec, YieldMetadata, description);
}

internal class AnySatisfiedSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    : HigherOrderReasonsSpecBuilderBase<TModel, TUnderlyingMetadata>
{
    public override SpecBase<IEnumerable<TModel>, string> CreateSpec(string description) =>
        new AnySatisfiedSpec<TModel, string, TUnderlyingMetadata>(
            underlyingSpec, 
            YieldReasons, 
            description.ThrowIfNullOrWhitespace(nameof(description)));
    
    public override SpecBase<IEnumerable<TModel>, string> CreateSpec() => 
        new AnySatisfiedSpec<TModel, string, TUnderlyingMetadata>(
            underlyingSpec, 
            YieldReasons);
}