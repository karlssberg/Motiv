namespace Motiv.HigherOrderProposition;

/// <summary>
/// The meta-result of a higher order evaluation.  It provides properties for common
/// </summary>
/// <typeparam name="TModel"></typeparam>
/// <typeparam name="TMetadata"></typeparam>
public class HigherOrderPolicyResultEvaluation<TModel, TMetadata>
{
    private readonly IReadOnlyList<PolicyResult<TModel, TMetadata>> _results;
    private readonly IReadOnlyList<PolicyResult<TModel, TMetadata>> _causalResults;

    private bool? _allSatisfied;
    private bool? _anySatisfied;
    private bool? _noneSatisfied;
    private IReadOnlyList<PolicyResult<TModel, TMetadata>>? _trueResults;
    private IReadOnlyList<PolicyResult<TModel, TMetadata>>? _falseResults;
    private IReadOnlyList<string>? _assertions;
    private IReadOnlyList<TMetadata>? _metadata;
    private IReadOnlyList<TMetadata>? _values;

    internal HigherOrderPolicyResultEvaluation(IReadOnlyList<PolicyResult<TModel, TMetadata>> results,
        IReadOnlyList<PolicyResult<TModel, TMetadata>> causalResults)
    {
        _results = results;
        _causalResults = causalResults;
    }

    /// <summary>
    /// Gets the underlying <see cref="PolicyResult{TModel,TMetadata}.Value" /> from each models' evaluation.
    /// </summary>
    public IEnumerable<TMetadata> Metadata => _metadata ??=
        _causalResults.SelectMany(result => result.MetadataTier.Metadata).DistinctWithOrderPreserved().ToArray();

    /// <summary>
    /// Gets a value indicating whether all results are satisfied.
    /// </summary>
    public bool AllSatisfied => _allSatisfied ??= _results.All(result => result.Satisfied);

    /// <summary>
    /// Gets a value indicating whether any results are satisfied.
    /// </summary>
    public bool AnySatisfied => _anySatisfied ??= _results.Any(result => result.Satisfied);

    /// <summary>
    /// Gets a value indicating whether none of the results are satisfied.
    /// </summary>
    public bool NoneSatisfied => _noneSatisfied ??= _results.All(result => !result.Satisfied);

    /// <summary>
    /// Gets the list of all models.
    /// </summary>
    public IReadOnlyList<TModel> Models => field ??= _results.Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets the list of models where the result is true.
    /// </summary>
    public IReadOnlyList<TModel> TrueModels => field ??=
        (_trueResults ??= _results.WhereTrue().ToArray()).Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets the list of models where the result is false.
    /// </summary>
    public IReadOnlyList<TModel> FalseModels => field ??=
        (_falseResults ??= _results.WhereFalse().ToArray()).Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets the list of causal models.
    /// </summary>
    public IReadOnlyList<TModel> CausalModels => field ??= _causalResults.Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets the metadata associated with the evaluation.
    /// </summary>
    public IEnumerable<TMetadata> Values => _values ??=
        _causalResults.Select(result => result.Value).ToArray();

    /// <summary>
    /// Gets the assertions made during the evaluation.
    /// </summary>
    public IEnumerable<string> Assertions => _assertions ??=
        _causalResults.SelectMany(result => result.Explanation.Assertions).DistinctWithOrderPreserved().ToArray();

    /// <summary>
    /// Gets the list of all results.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> Results => _results;

    /// <summary>
    /// Gets the list of results where the result is true.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> TrueResults =>
        _trueResults ??= _results.WhereTrue().ToArray();

    /// <summary>
    /// Gets the list of results where the result is false.
    /// </summary>
    public IEnumerable<PolicyResult<TModel, TMetadata>> FalseResults =>
        _falseResults ??= _results.WhereFalse().ToArray();

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
    public int TrueCount => (_trueResults ??= _results.WhereTrue().ToArray()).Count;

    /// <summary>
    /// Gets the count of false results.
    /// </summary>
    public int FalseCount => (_falseResults ??= _results.WhereFalse().ToArray()).Count;

    /// <summary>
    /// Gets the count of causal results.
    /// </summary>
    public int CausalCount => _causalResults.Count;
}
