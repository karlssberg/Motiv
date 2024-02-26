using System.Collections;

namespace Karlssberg.Motiv.Propositions;

public sealed record BooleanCollectionResult<TModel, TMetadata>(
    bool IsSatisfied,
    ICollection<BooleanResult<TModel, TMetadata>> AllResults,
    ICollection<BooleanResult<TModel, TMetadata>> CausalResults)
    : IEnumerable<BooleanResult<TModel, TMetadata>>
{

    public bool IsSatisfied { get; } = IsSatisfied;
    
    public ICollection<BooleanResult<TModel, TMetadata>> AllResults { get; } = AllResults;
    
    public ICollection<BooleanResult<TModel, TMetadata>> CausalResults { get; } = CausalResults;
    
    
    public IEnumerable<TModel> AllModels => AllResults
        .Select(result => result.Model);
    
    public IEnumerable<TModel> CausalModels => CausalResults
        .Select(result => result.Model);
    
    public IEnumerable<TMetadata> Metadata => CausalResults
        .SelectMany(result => result.Metadata);
    
    public IEnumerable<string> Reasons => CausalResults
        .SelectMany(result => result.Reason.Assertions);

    public IEnumerable<BooleanResult<TModel, TMetadata>> WhereTrue() => AllResults
        .Where(result => result.Satisfied);
    
    public IEnumerable<BooleanResult<TModel, TMetadata>> WhereFalse() => AllResults
        .Where(result => !result.Satisfied);

    public int TrueCount() => WhereTrue().Count();

    public int FalseCount() => WhereFalse().Count();
    
    public int CauseCount() => CausalResults.Count;
    
    public IEnumerator<BooleanResult<TModel, TMetadata>> GetEnumerator() => CausalResults.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

