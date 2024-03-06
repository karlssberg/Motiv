namespace Karlssberg.Motiv;

public class Evaluation<TModel, TMetadata>(
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
    
    public IEnumerable<TMetadata> Metadata => _lazyMetadata.Value;
    public IEnumerable<string> Assertions => _lazyAssertions.Value;

    public IEnumerable<BooleanResultBase<TMetadata>> AllResults=>  allResults;
    public IEnumerable<BooleanResultBase<TMetadata>> TrueResults => _lazyTrueResults.Value;
    public IEnumerable<BooleanResultBase<TMetadata>> FalseResults => _lazyFalseResults.Value;
    public IEnumerable<BooleanResultBase<TMetadata>> CausalResults { get; } = causalResults;

    public int AllCount => allResults.Count;
    public int TrueCount => _lazyTrueResults.Value.Count;
    public int FalseCount => _lazyFalseResults.Value.Count;
    public int CausalCount => causalResults.Count;
}