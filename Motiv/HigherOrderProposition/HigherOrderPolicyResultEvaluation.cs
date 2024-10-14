namespace Motiv.HigherOrderProposition;

/// <summary>
/// The meta-result of a higher order evaluation.  It provides properties for common
/// </summary>
/// <typeparam name="TModel"></typeparam>
/// <typeparam name="TMetadata"></typeparam>
public class HigherOrderPolicyResultEvaluation<TModel, TMetadata>
{
    private readonly Lazy<IReadOnlyList<TModel>> _lazyAllModels;

    private readonly Lazy<bool> _lazyAllSatisfied;
    private readonly Lazy<bool> _lazyAnySatisfied;
    private readonly Lazy<bool> _lazyNoneSatisfied;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyCausalModels;
    private readonly Lazy<IReadOnlyList<PolicyResult<TModel, TMetadata>>> _lazyTrueResults;
    private readonly Lazy<IReadOnlyList<PolicyResult<TModel, TMetadata>>> _lazyFalseResults;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyTrueModels;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyFalseModels;
    private readonly Lazy<IReadOnlyList<string>> _lazyAssertions;
    private readonly Lazy<IReadOnlyList<TMetadata>> _lazyMetadata;

    private readonly IReadOnlyList<PolicyResult<TModel, TMetadata>> _results;
    private readonly IReadOnlyList<PolicyResult<TModel, TMetadata>> _causalResults;

    internal HigherOrderPolicyResultEvaluation(IReadOnlyList<PolicyResult<TModel, TMetadata>> results,
        IReadOnlyList<PolicyResult<TModel, TMetadata>> causalResults)
    {
        _results = results;
        _causalResults = causalResults;

        _lazyAllModels = new Lazy<IReadOnlyList<TModel>>(() =>
            results.Select(result => result.Model).ToArray());
        _lazyAllSatisfied = new Lazy<bool>(() =>
            results.All(result => result.Satisfied));
        _lazyAnySatisfied = new Lazy<bool>(() =>
            results.Any(result => result.Satisfied));
        _lazyNoneSatisfied = new Lazy<bool>(() =>
            results.All(result => !result.Satisfied));

        _lazyCausalModels = new Lazy<IReadOnlyList<TModel>>(() =>
            causalResults.Select(result => result.Model).ToArray());
        _lazyTrueResults = new Lazy<IReadOnlyList<PolicyResult<TModel, TMetadata>>>(() =>
            results.WhereTrue().ToArray());
        _lazyFalseResults = new Lazy<IReadOnlyList<PolicyResult<TModel, TMetadata>>>(() =>
            results.WhereFalse().ToArray());
        _lazyTrueModels = new Lazy<IReadOnlyList<TModel>>(() =>
            results.WhereTrue().Select(result => result.Model).ToArray());
        _lazyFalseModels = new Lazy<IReadOnlyList<TModel>>(() =>
            results.WhereFalse().Select(result => result.Model).ToArray());
        _lazyAssertions = new Lazy<IReadOnlyList<string>>(() =>
            causalResults.SelectMany(result => result.Explanation.Assertions).DistinctWithOrderPreserved().ToArray());
        _lazyMetadata = new Lazy<IReadOnlyList<TMetadata>>(() =>
            causalResults.SelectMany(result => result.MetadataTier.Metadata).DistinctWithOrderPreserved().ToArray());
    }

    /// <summary>
    /// Gets the underlying <see cref="PolicyResult{TModel,TMetadata}.Value" /> from each models' evaluation.
    /// </summary>
    public IEnumerable<TMetadata> Metadata => _lazyMetadata.Value;

    /// <summary>
    /// Gets a value indicating whether all results are satisfied.
    /// </summary>
    public bool AllSatisfied => _lazyAllSatisfied.Value;

    /// <summary>
    /// Gets a value indicating whether any results are satisfied.
    /// </summary>
    public bool AnySatisfied => _lazyAnySatisfied.Value;

    /// <summary>
    /// Gets a value indicating whether none of the results are satisfied.
    /// </summary>
    public bool NoneSatisfied => _lazyNoneSatisfied.Value;

    /// <summary>
    /// Gets the list of all models.
    /// </summary>
    public IReadOnlyList<TModel> Models => _lazyAllModels.Value;

    /// <summary>
    /// Gets the list of models where the result is true.
    /// </summary>
    public IReadOnlyList<TModel> TrueModels => _lazyTrueModels.Value;

    /// <summary>
    /// Gets the list of models where the result is false.
    /// </summary>
    public IReadOnlyList<TModel> FalseModels => _lazyFalseModels.Value;

    /// <summary>
    /// Gets the list of causal models.
    /// </summary>
    public IReadOnlyList<TModel> CausalModels => _lazyCausalModels.Value;

    /// <summary>
    /// Gets the metadata associated with the evaluation.
    /// </summary>
    public IEnumerable<TMetadata> Values => _causalResults.Select(result => result.Value);

    /// <summary>
    /// Gets the assertions made during the evaluation.
    /// </summary>
    public IEnumerable<string> Assertions => _lazyAssertions.Value;

    /// <summary>
    /// Gets the list of all results.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> Results=>  _results;

    /// <summary>
    /// Gets the list of results where the result is true.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> TrueResults => _lazyTrueResults.Value;

    /// <summary>
    /// Gets the list of results where the result is false.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> FalseResults => _lazyFalseResults.Value;

    /// <summary>
    /// Gets the list of causal results.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> CausalResults => _causalResults;

    /// <summary>
    /// Gets the total count of results.
    /// </summary>
    public int Count => _results.Count;

    /// <summary>
    /// Gets the count of true results.
    /// </summary>
    public int TrueCount => _lazyTrueResults.Value.Count;

    /// <summary>
    /// Gets the count of false results.
    /// </summary>
    public int FalseCount => _lazyFalseResults.Value.Count;

    /// <summary>
    /// Gets the count of causal results.
    /// </summary>
    public int CausalCount => _causalResults.Count;
}
