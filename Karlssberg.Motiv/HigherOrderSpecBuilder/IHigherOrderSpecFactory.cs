namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IHigherOrderSpecFactory<TModel, TMetadata>
{
    SpecBase<IEnumerable<TModel>, TMetadata>   CreateSpec(string description);
    SpecBase<IEnumerable<TModel>, TMetadata>   CreateSpec();
}

public interface IHigherOrderSpecFactory<TModel>
{
    SpecBase<IEnumerable<TModel>, string> CreateSpec(string description);
    
    SpecBase<IEnumerable<TModel>, string> CreateSpec();
}