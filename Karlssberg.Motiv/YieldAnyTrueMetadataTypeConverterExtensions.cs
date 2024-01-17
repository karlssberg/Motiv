using Karlssberg.Motiv.HigherOrderSpecBuilder;

namespace Karlssberg.Motiv;

public static class YieldAnyTrueMetadataTypeConverterExtensions
{
        
    public static IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TModel, TAltMetadata, TMetadata>(
        this IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> converter,
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, TAltMetadata> metadata) =>
        converter.YieldWhenAnyTrue<TAltMetadata>((results) => [metadata(results)]);
         
    public static IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TModel, TAltMetadata, TMetadata>(
        this IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> converter,
        Func<IEnumerable<TAltMetadata>> metadata)=>
        converter.YieldWhenAnyTrue((results) => metadata());
         
    public static IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TModel, TAltMetadata, TMetadata>(
        this IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> converter,
        TAltMetadata metadata) =>
        converter.YieldWhenAnyTrue<TAltMetadata>((results) => [metadata]);
}