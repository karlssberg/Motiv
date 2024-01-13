namespace Karlssberg.Motiv.CollectionBuilder;

public interface ICollectionSpecBuilder<TModel, TMetadata> :
    IYieldTrueMetadata<TModel, TMetadata>,
    IYieldFalseMetadata<TModel, TMetadata>
{
}