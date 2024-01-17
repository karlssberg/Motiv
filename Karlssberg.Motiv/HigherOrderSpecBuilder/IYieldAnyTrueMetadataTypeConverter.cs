namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> 
{
    IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);

}