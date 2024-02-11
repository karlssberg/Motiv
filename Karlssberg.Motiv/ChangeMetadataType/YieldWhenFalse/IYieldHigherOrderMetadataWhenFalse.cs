namespace Karlssberg.Motiv.ChangeMetadataType.YieldWhenFalse;

public interface IYieldHigherOrderMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    SpecBase<IEnumerable<TModel>, TMetadata> WhenFalse(Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse);
}