namespace Karlssberg.Motiv;

public class FirstOrderEvaluation<TModel, TMetadata>(
    bool satisfied,
    TModel model,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> allResults,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> causalResults)
{

    private readonly Lazy<bool> _lazyAllSatisfied = new(() =>
        allResults.All(result => result.Satisfied));
    private readonly Lazy<bool> _lazyNoneSatisfied = new(() =>
        allResults.All(result => !result.Satisfied));

    private readonly Lazy<IReadOnlyCollection<BooleanResultBase<TMetadata>>> _lazyTrueResults = new(() =>
        allResults.Where(result => result.Satisfied).ToArray());
    private readonly Lazy<IReadOnlyCollection<BooleanResultBase<TMetadata>>> _lazyFalseResults = new(() =>
        allResults.Where(result => !result.Satisfied).ToArray());
    
    private readonly Lazy<IReadOnlyCollection<string>> _lazyAssertions = new(() =>
        causalResults.SelectMany(result => result.Assertions).ToArray());
    private readonly Lazy<IReadOnlyCollection<TMetadata>> _lazyMetadata = new(() =>
        causalResults.SelectMany(result => result.Metadata).ToArray());
    
    public TModel Model => model;

    public bool Satisfied => satisfied;
    public bool AreAllSatisfied => _lazyAllSatisfied.Value;
    public bool AreNoneSatisfied => _lazyNoneSatisfied.Value;
    
    public IReadOnlyCollection<TMetadata> Metadata => _lazyMetadata.Value;
    public IReadOnlyCollection<string> Assertions => _lazyAssertions.Value;

    public IReadOnlyCollection<BooleanResultBase<TMetadata>> All=>  allResults;
    public IReadOnlyCollection<BooleanResultBase<TMetadata>> AllTrue => _lazyTrueResults.Value;
    public IReadOnlyCollection<BooleanResultBase<TMetadata>> AllFalse => _lazyFalseResults.Value;
    public IReadOnlyCollection<BooleanResultBase<TMetadata>> AllCauses { get; } = causalResults;

    public int TrueCount => _lazyTrueResults.Value.Count;
    public int FalseCount => _lazyFalseResults.Value.Count;
    public int Count => allResults.Count;
}