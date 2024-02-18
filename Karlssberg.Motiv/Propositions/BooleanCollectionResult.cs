using System.Collections;

namespace Karlssberg.Motiv.Propositions;

public record BooleanCollectionResult<TModel, TMetadata>(
    IEnumerable<BooleanResult<TModel, TMetadata>> AllResults,
    IEnumerable<BooleanResult<TModel, TMetadata>> CausalResults)
    : IEnumerable<BooleanResult<TModel, TMetadata>>
{
    
    public IEnumerable<BooleanResult<TModel, TMetadata>> AllResults { get; } = AllResults;
    public IEnumerable<BooleanResult<TModel, TMetadata>> CausalResults { get; } = CausalResults;
    
    
    public IEnumerable<TModel> AllModels => AllResults.Select(result => result.Model);
    public IEnumerable<TModel> CausalModels => CausalResults.Select(result => result.Model);
    
    public IEnumerable<TMetadata> Metadata => CausalResults.SelectMany(result => result.Metadata);
    public IEnumerable<string> Reasons => CausalResults.SelectMany(result => result.Explanation.Reasons);

    public IEnumerable<BooleanResult<TModel, TMetadata>> WhereTrue() => AllResults.Where(result => result.Satisfied);
    public IEnumerable<BooleanResult<TModel, TMetadata>> WhereFalse() => AllResults
        .Where(result => !result.Satisfied);

    public int TrueCount() => WhereTrue().Count();

    public int FalseCount() => WhereFalse().Count();
    public int DeterminativeCount() => CausalResults.Count();
    

    public IEnumerator<BooleanResult<TModel, TMetadata>> GetEnumerator() => AllResults.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AllResults).GetEnumerator();
}