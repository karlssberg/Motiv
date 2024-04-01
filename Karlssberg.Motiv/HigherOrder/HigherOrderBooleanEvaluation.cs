namespace Karlssberg.Motiv.HigherOrder;

public sealed class HigherOrderBooleanEvaluation<TModel>(
    IReadOnlyCollection<ModelResult<TModel>> allResults,
    IReadOnlyCollection<ModelResult<TModel>> causalResults)
{
    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyAllModels = new(() =>
        allResults.Select(result => result.Model).ToArray());

    private readonly Lazy<bool> _lazyAllSatisfied = new(() =>
        allResults.All(result => result.Satisfied));
    private readonly Lazy<bool> _lazyNoneSatisfied = new(() =>
        allResults.All(result => !result.Satisfied));

    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyCausalModels = new(() =>
        causalResults.Select(result => result.Model).ToArray());
    
    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyTrueModels = new(() =>
        allResults
            .Where(r => r.Satisfied)
            .Select(result => result.Model)
            .ToArray());
    
    private readonly Lazy<IReadOnlyCollection<TModel>> _lazyFalseModels = new(() =>
        allResults
            .Where(r => !r.Satisfied)
            .Select(result => result.Model)
            .ToArray());
    public bool AllSatisfied => _lazyAllSatisfied.Value;
    public bool NoneSatisfied => _lazyNoneSatisfied.Value;

    public IReadOnlyCollection<TModel> Models => _lazyAllModels.Value;
    public IReadOnlyCollection<TModel> TrueModels => _lazyTrueModels.Value;
    public IReadOnlyCollection<TModel> FalseModels => _lazyFalseModels.Value;
    public IReadOnlyCollection<TModel> CausalModels => _lazyCausalModels.Value;
    
    public int Count => allResults.Count;
    public int TrueCount => _lazyTrueModels.Value.Count;
    public int FalseCount => _lazyFalseModels.Value.Count;
    public int CausalCount => causalResults.Count;
}