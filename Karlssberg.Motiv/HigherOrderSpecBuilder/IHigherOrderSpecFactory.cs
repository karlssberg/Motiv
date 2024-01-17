namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IHigherOrderSpecFactory<TModel, TMetadata> 
{
    SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec();
    SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description);
}