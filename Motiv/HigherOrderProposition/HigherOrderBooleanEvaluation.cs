namespace Motiv.HigherOrderProposition;

/// <summary>
/// Represents a higher order boolean evaluation for a model.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public sealed class HigherOrderBooleanEvaluation<TModel>
{
    private readonly IReadOnlyList<ModelResult<TModel>> _allResults;
    private readonly IReadOnlyList<ModelResult<TModel>> _causalResults;

    // Lazy loaded properties for performance optimization
    private readonly Lazy<bool> _lazyAllSatisfied;
    private readonly Lazy<bool> _lazyAnySatisfied;
    private readonly Lazy<bool> _lazyNoneSatisfied;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyAllModels;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyCausalModels;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyTrueModels;
    private readonly Lazy<IReadOnlyList<TModel>> _lazyFalseModels;

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
        
        _lazyAllModels = new Lazy<IReadOnlyList<TModel>>(() =>
            allResults.Select(result => result.Model).ToArray());

        _lazyAllSatisfied = new Lazy<bool>(() =>
            allResults.All(result => result.Satisfied));
        
        _lazyAnySatisfied = new Lazy<bool>(() =>
            allResults.Any(result => result.Satisfied));

        _lazyNoneSatisfied = new Lazy<bool>(() =>
            allResults.All(result => !result.Satisfied));

        _lazyCausalModels = new Lazy<IReadOnlyList<TModel>>(() =>
            causalResults.Select(result => result.Model).ToArray());

        _lazyTrueModels = new Lazy<IReadOnlyList<TModel>>(() =>
            allResults
                .Where(r => r.Satisfied)
                .Select(result => result.Model)
                .ToArray());

        _lazyFalseModels = new Lazy<IReadOnlyList<TModel>>(() =>
            allResults
                .Where(r => !r.Satisfied)
                .Select(result => result.Model)
                .ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether all results are satisfied.
    /// </summary>
    public bool AllSatisfied => _lazyAllSatisfied.Value;
    
    /// <summary>
    /// Gets a value indicating whether any of the results are satisfied.
    /// </summary>
    public bool AnySatisfied => _lazyAnySatisfied.Value;

    /// <summary>
    /// Gets a value indicating whether none of the results are satisfied.
    /// </summary>
    public bool NoneSatisfied => _lazyNoneSatisfied.Value;

    /// <summary>
    /// Gets all models.
    /// </summary>
    public IReadOnlyList<TModel> Models => _lazyAllModels.Value;

    /// <summary>
    /// Gets all models that are true.
    /// </summary>
    public IReadOnlyList<TModel> TrueModels => _lazyTrueModels.Value;

    /// <summary>
    /// Gets all models that are false.
    /// </summary>
    public IReadOnlyList<TModel> FalseModels => _lazyFalseModels.Value;

    /// <summary>
    /// Gets all causal models.
    /// </summary>
    public IReadOnlyList<TModel> CausalModels => _lazyCausalModels.Value;

    /// <summary>
    /// Gets the total count of all results.
    /// </summary>
    public int Count => _allResults.Count;

    /// <summary>
    /// Gets the count of true models.
    /// </summary>
    public int TrueCount => _lazyTrueModels.Value.Count;

    /// <summary>
    /// Gets the count of false models.
    /// </summary>
    public int FalseCount => _lazyFalseModels.Value.Count;

    /// <summary>
    /// Gets the count of causal results.
    /// </summary>
    public int CausalCount => _causalResults.Count;
}