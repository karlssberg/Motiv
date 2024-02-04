namespace Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

public interface IYieldHigherOrderMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    SpecBase<IEnumerable<TModel>, TMetadata> YieldWhenFalse(Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse);
}