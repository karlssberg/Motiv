namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IYieldAnythingTypeConverter<TModel, TMetadata>
{
    IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnything<TAltMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);
    // IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnything<TAltMetadata>(
    //     Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, TMetadata> metadata;
    // IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnything<TAltMetadata>(
    //     Func<bool, IEnumerable<TMetadata>> metadata);
}