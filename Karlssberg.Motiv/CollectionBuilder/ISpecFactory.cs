namespace Karlssberg.Motiv.CollectionBuilder;

public interface ISpecFactory<TModel, TMetadata> 
{
    SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec();
}