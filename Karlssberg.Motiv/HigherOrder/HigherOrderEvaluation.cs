using System.Collections;

namespace Karlssberg.Motiv.HigherOrder;

public sealed class HigherOrderEvaluation<TModel, TMetadata>(
    IReadOnlyCollection<BooleanResult<TModel, TMetadata>> allResults,
    IReadOnlyCollection<BooleanResult<TModel, TMetadata>> causalResults)
{
    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyAllModels = new(() =>
        allResults.Select(result => result.Model).ToArray());

    private readonly Lazy<bool> _lazyAllSatisfied = new(() =>
        allResults.All(result => result.Satisfied));
    private readonly Lazy<bool> _lazyNoneSatisfied = new(() =>
        allResults.All(result => !result.Satisfied));

    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyCausalModels = new(() =>
        causalResults.Select(result => result.Model).ToArray());

    private readonly Lazy<IReadOnlyCollection<BooleanResult<TModel, TMetadata>>> _lazyTrueResults = new(() =>
        allResults.WhereTrue().ToArray());
    private readonly Lazy<IReadOnlyCollection<BooleanResult<TModel, TMetadata>>> _lazyFalseResults = new(() =>
        allResults.WhereFalse().ToArray());
    
    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyTrueModels = new(() =>
        allResults.WhereTrue().Select(result => result.Model).ToArray());
    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyFalseModels = new(() =>
        allResults.WhereFalse().Select(result => result.Model).ToArray());

    private readonly Lazy<IReadOnlyCollection<string>> _lazyAssertions = new(() =>
        causalResults.SelectMany(result => result.Assertions).ToArray());
    private readonly Lazy<IReadOnlyCollection<TMetadata>> _lazyMetadata = new(() =>
        causalResults.SelectMany(result => result.MetadataTree).ToArray());
    public bool AllSatisfied => _lazyAllSatisfied.Value;
    public bool NoneSatisfied => _lazyNoneSatisfied.Value;

    public IReadOnlyCollection<TModel> AllModels => _lazyAllModels.Value;
    public IReadOnlyCollection<TModel> TrueModels => _lazyTrueModels.Value;
    public IReadOnlyCollection<TModel> FalseModels => _lazyFalseModels.Value;
    public IReadOnlyCollection<TModel> CausalModels => _lazyCausalModels.Value;
    
    public IEnumerable<TMetadata> Metadata => _lazyMetadata.Value;
    public IEnumerable<string> Assertions => _lazyAssertions.Value;

    public IEnumerable<BooleanResult<TModel, TMetadata>> AllResults=>  allResults;
    public IEnumerable<BooleanResult<TModel, TMetadata>> TrueResults => _lazyTrueResults.Value;
    public IEnumerable<BooleanResult<TModel, TMetadata>> FalseResults => _lazyFalseResults.Value;
    public IEnumerable<BooleanResult<TModel, TMetadata>> CausalResults { get; } = causalResults;
    
    public int AllCount => allResults.Count;
    public int TrueCount => _lazyTrueResults.Value.Count;
    public int FalseCount => _lazyFalseResults.Value.Count;
    public int CausalCount => causalResults.Count;
}