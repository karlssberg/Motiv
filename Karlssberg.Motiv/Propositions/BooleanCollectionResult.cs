using System.Collections;

namespace Karlssberg.Motiv.Propositions;

public record BooleanCollectionResult<TModel, TMetadata>(
    IEnumerable<BooleanResult<TModel, TMetadata>> AllResults,
    IEnumerable<BooleanResult<TModel, TMetadata>> Causes)
    : IEnumerable<BooleanResult<TModel, TMetadata>>
{
    
    public IEnumerable<BooleanResult<TModel, TMetadata>> AllResults { get; } = AllResults;
    public IEnumerable<BooleanResult<TModel, TMetadata>> Causes { get; } = Causes;
    
    
    public IEnumerable<TModel> AllModels => AllResults.Select(result => result.Model);
    public IEnumerable<TModel> DeterminativeModels => Causes.Select(result => result.Model);
    
    public IEnumerable<TMetadata> Metadata => Causes.SelectMany(result => result.GetMetadata());
    public IEnumerable<string> Reasons => Causes.SelectMany(result => result.Reasons);

    public IEnumerable<BooleanResult<TModel, TMetadata>> WhereTrue() => AllResults.Where(result => result.Satisfied);
    public IEnumerable<BooleanResult<TModel, TMetadata>> WhereFalse() => AllResults
        .Where(result => !result.Satisfied);

    public int TrueCount() => WhereTrue().Count();

    public int FalseCount() => WhereFalse().Count();
    public int DeterminativeCount() => Causes.Count();
    

    public IEnumerator<BooleanResult<TModel, TMetadata>> GetEnumerator() => AllResults.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AllResults).GetEnumerator();


}