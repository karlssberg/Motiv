using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

public interface IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata>
{
    IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TAltMetadata>> metadata);
}