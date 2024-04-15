namespace Karlssberg.Motiv.HigherOrderProposition;

public struct HigherOrderEvaluation<TModel, TMetadata>
{
    private readonly Lazy<IReadOnlyList<TModel>> _lazyAllModels;

    private readonly Lazy<bool> _lazyAllSatisfied;
    private readonly Lazy<bool> _lazyNoneSatisfied;

    private readonly Lazy<IReadOnlyList<TModel>> _lazyCausalModels;

    private readonly Lazy<IReadOnlyList<BooleanResult<TModel, TMetadata>>> _lazyTrueResults;
    private readonly Lazy<IReadOnlyList<BooleanResult<TModel, TMetadata>>> _lazyFalseResults;
    
    private readonly Lazy<IReadOnlyList<TModel>> _lazyTrueModels;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyFalseModels;

    private readonly Lazy<IReadOnlyList<string>> _lazyAssertions;
    private readonly Lazy<IReadOnlyList<TMetadata>> _lazyMetadata;

    private readonly IReadOnlyList<BooleanResult<TModel, TMetadata>> _results;
    private readonly IReadOnlyList<BooleanResult<TModel, TMetadata>> _causalResults;

    internal HigherOrderEvaluation(IReadOnlyList<BooleanResult<TModel, TMetadata>> results,
        IReadOnlyList<BooleanResult<TModel, TMetadata>> causalResults)
    {
        _results = results;
        _causalResults = causalResults;
        _lazyAllModels = new Lazy<IReadOnlyList<TModel>>(() =>
            results.Select(result => result.Model).ToArray());
        _lazyAllSatisfied = new Lazy<bool>(() =>
            results.All(result => result.Satisfied));
        _lazyNoneSatisfied = new Lazy<bool>(() =>
            results.All(result => !result.Satisfied));
        _lazyCausalModels = new Lazy<IReadOnlyList<TModel>>(() =>
            causalResults.Select(result => result.Model).ToArray());
        _lazyTrueResults = new Lazy<IReadOnlyList<BooleanResult<TModel, TMetadata>>>(() =>
            results.WhereTrue().ToArray());
        _lazyFalseResults = new Lazy<IReadOnlyList<BooleanResult<TModel, TMetadata>>>(() =>
            results.WhereFalse().ToArray());
        _lazyTrueModels = new Lazy<IReadOnlyList<TModel>>(() =>
            results.WhereTrue().Select(result => result.Model).ToArray());
        _lazyFalseModels = new Lazy<IReadOnlyList<TModel>>(() =>
            results.WhereFalse().Select(result => result.Model).ToArray());
        _lazyAssertions = new Lazy<IReadOnlyList<string>>(() =>
            causalResults.SelectMany(result => result.Assertions).ToArray());
        _lazyMetadata = new Lazy<IReadOnlyList<TMetadata>>(() =>
            causalResults.SelectMany(result => result.MetadataTree).ToArray());
        CausalResults = causalResults;
    }

    public bool AllSatisfied => _lazyAllSatisfied.Value;
    public bool NoneSatisfied => _lazyNoneSatisfied.Value;

    public IReadOnlyList<TModel> Models => _lazyAllModels.Value;
    public IReadOnlyList<TModel> TrueModels => _lazyTrueModels.Value;
    public IReadOnlyList<TModel> FalseModels => _lazyFalseModels.Value;
    public IReadOnlyList<TModel> CausalModels => _lazyCausalModels.Value;
    
    public IEnumerable<TMetadata> Metadata => _lazyMetadata.Value;
    public IEnumerable<string> Assertions => _lazyAssertions.Value;

    public IEnumerable<BooleanResult<TModel, TMetadata>> Results=>  _results;
    public IEnumerable<BooleanResult<TModel, TMetadata>> TrueResults => _lazyTrueResults.Value;
    public IEnumerable<BooleanResult<TModel, TMetadata>> FalseResults => _lazyFalseResults.Value;
    public IEnumerable<BooleanResult<TModel, TMetadata>> CausalResults { get; }
    
    public int Count => _results.Count;
    public int TrueCount => _lazyTrueResults.Value.Count;
    public int FalseCount => _lazyFalseResults.Value.Count;
    public int CausalCount => _causalResults.Count;
}