namespace Karlssberg.Motiv.HigherOrder;

public sealed class HigherOrderBooleanEvaluation<TModel>(
    IReadOnlyList<ModelResult<TModel>> allResults,
    IReadOnlyList<ModelResult<TModel>> causalResults)
{
    private readonly Lazy<IReadOnlyList<TModel>> _lazyAllModels = new(() =>
        allResults.Select(result => result.Model).ToArray());

    private readonly Lazy<bool> _lazyAllSatisfied = new(() =>
        allResults.All(result => result.Satisfied));
    private readonly Lazy<bool> _lazyNoneSatisfied = new(() =>
        allResults.All(result => !result.Satisfied));

    private readonly Lazy<IReadOnlyList<TModel>> _lazyCausalModels = new(() =>
        causalResults.Select(result => result.Model).ToArray());
    
    private readonly Lazy<IReadOnlyList<TModel>> _lazyTrueModels = new(() =>
        allResults
            .Where(r => r.Satisfied)
            .Select(result => result.Model)
            .ToArray());
    
    private readonly Lazy<IReadOnlyList<TModel>> _lazyFalseModels = new(() =>
        allResults
            .Where(r => !r.Satisfied)
            .Select(result => result.Model)
            .ToArray());
    public bool AllSatisfied => _lazyAllSatisfied.Value;
    public bool NoneSatisfied => _lazyNoneSatisfied.Value;

    public IReadOnlyList<TModel> Models => _lazyAllModels.Value;
    public IReadOnlyList<TModel> TrueModels => _lazyTrueModels.Value;
    public IReadOnlyList<TModel> FalseModels => _lazyFalseModels.Value;
    public IReadOnlyList<TModel> CausalModels => _lazyCausalModels.Value;
    
    public int Count => allResults.Count;
    public int TrueCount => _lazyTrueModels.Value.Count;
    public int FalseCount => _lazyFalseModels.Value.Count;
    public int CausalCount => causalResults.Count;
}