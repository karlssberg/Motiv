namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

public interface IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata>
{
    IHigherOrderSpecFactory<TModel, TMetadata> Yield<TMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}