namespace Motiv.HigherOrderProposition;

/// <summary>
/// Represents a higher order boolean evaluation for a model.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public sealed class HigherOrderBooleanEvaluation<TModel>
{
    private readonly IReadOnlyList<ModelResult<TModel>> _allResults;
    private readonly IReadOnlyList<ModelResult<TModel>> _causalResults;

    private bool? _allSatisfied;
    private bool? _anySatisfied;
    private bool? _noneSatisfied;
    private IReadOnlyList<TModel>? _allModels;
    private IReadOnlyList<TModel>? _causalModels;
    private IReadOnlyList<TModel>? _trueModels;
    private IReadOnlyList<TModel>? _falseModels;

    /// <summary>
    /// Initializes a new instance of the <see cref="HigherOrderBooleanEvaluation{TModel}"/> class.
    /// </summary>
    /// <param name="allResults">All model results.</param>
    /// <param name="causalResults">Causal model results.</param>
    internal HigherOrderBooleanEvaluation(
        IReadOnlyList<ModelResult<TModel>> allResults,
        IReadOnlyList<ModelResult<TModel>> causalResults)
    {
        _allResults = allResults;
        _causalResults = causalResults;
    }

    /// <summary>
    /// Gets a value indicating whether all results are satisfied.
    /// </summary>
    public bool AllSatisfied => _allSatisfied ??= _allResults.All(result => result.Satisfied);

    /// <summary>
    /// Gets a value indicating whether any of the results are satisfied.
    /// </summary>
    public bool AnySatisfied => _anySatisfied ??= _allResults.Any(result => result.Satisfied);

    /// <summary>
    /// Gets a value indicating whether none of the results are satisfied.
    /// </summary>
    public bool NoneSatisfied => _noneSatisfied ??= _allResults.All(result => !result.Satisfied);

    /// <summary>
    /// Gets all models.
    /// </summary>
    public IReadOnlyList<TModel> Models => _allModels ??= _allResults.Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets all models that are true.
    /// </summary>
    public IReadOnlyList<TModel> TrueModels => _trueModels ??= _allResults.WhereTrue().Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets all models that are false.
    /// </summary>
    public IReadOnlyList<TModel> FalseModels => _falseModels ??= _allResults.WhereFalse().Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets all causal models.
    /// </summary>
    public IReadOnlyList<TModel> CausalModels => _causalModels ??= _causalResults.Select(result => result.Model).ToArray();

    /// <summary>
    /// Gets the total count of all results.
    /// </summary>
    public int Count => _allResults.Count;

    /// <summary>
    /// Gets the count of true models.
    /// </summary>
    public int TrueCount => TrueModels.Count;

    /// <summary>
    /// Gets the count of false models.
    /// </summary>
    public int FalseCount => FalseModels.Count;

    /// <summary>
    /// Gets the count of causal results.
    /// </summary>
    public int CausalCount => _causalResults.Count;
}
