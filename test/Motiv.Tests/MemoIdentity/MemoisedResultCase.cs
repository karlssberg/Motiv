namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// One public-API route to a result class, and the check that the result's memoised
/// <see cref="BooleanResultBase.Description" />, <see cref="BooleanResultBase.Explanation" /> and
/// <see cref="BooleanResultBase{TMetadata}.MetadataTier" /> — and what is read through them, its causes,
/// assertions, reasons and values — are the same instance on every read (#294).
/// </summary>
/// <remarks>
/// <para>
/// The identity is load-bearing rather than cosmetic. The folds memoise on these objects themselves —
/// a description's folded reason, an explanation's underlying explanations and a metadata node's
/// underlying nodes live on the node instance — and the justification and root-values walks key their
/// memos by reference. A property that built a fresh node per read would re-fold on every read and
/// expand a sub-result shared across a composition once per path to it.
/// </para>
/// <para>
/// Each case also pins the class it reaches by name, so a builder change that silently re-routes a
/// case to a different result class fails here instead of quietly dropping that class's coverage.
/// </para>
/// </remarks>
public sealed class MemoisedResultCase
{
    private static readonly (string Member, Func<BooleanResultBase, object> Read)[] Readers =
    [
        ("Description", r => r.Description),
        ("Explanation", r => r.Explanation),
        ("Causes", r => r.Causes),
        ("Underlying", r => r.Underlying),
        ("Assertions", r => r.Assertions),
        ("SubAssertions", r => r.SubAssertions),
        ("AllSubAssertions", r => r.AllSubAssertions),
        ("UnderlyingReasons", r => r.UnderlyingReasons),
        ("Reason", r => r.Reason),
        ("Justification", r => r.Justification),
        ("Explanation.Assertions", r => r.Explanation.Assertions),
        ("Explanation.ToString()", r => r.Explanation.ToString()),
        ("Explanation.Underlying", r => r.Explanation.Underlying),
        ("Explanation.AllUnderlying", r => r.Explanation.AllUnderlying),
        ("Description.Reason", r => r.Description.Reason),
        ("Description.Justification", r => r.Description.Justification),
    ];

    private readonly Func<Task<BooleanResultBase>> _evaluate;
    private readonly (string Member, Func<BooleanResultBase, object> Read)[] _metadataReaders;
    private readonly string[] _notMemoised;

    private MemoisedResultCase(
        string name,
        string resultType,
        Func<Task<BooleanResultBase>> evaluate,
        (string Member, Func<BooleanResultBase, object> Read)[] metadataReaders,
        string[]? notMemoised)
    {
        Name = name;
        ResultType = resultType;
        _evaluate = evaluate;
        _metadataReaders = metadataReaders;
        _notMemoised = notMemoised ?? [];
    }

    /// <summary>The theory-data key; unique within a test class.</summary>
    public string Name { get; }

    /// <summary>The result class the case must reach, without its generic arity.</summary>
    public string ResultType { get; }

    /// <param name="notMemoised">Members the result class deliberately rebuilds on each read.</param>
    public static MemoisedResultCase Of<TMetadata>(
        string name,
        string resultType,
        Func<BooleanResultBase<TMetadata>> evaluate,
        string[]? notMemoised = null) =>
        new(name, resultType, () => Task.FromResult<BooleanResultBase>(evaluate()), MetadataReadersOf<TMetadata>(), notMemoised);

    /// <param name="notMemoised">Members the result class deliberately rebuilds on each read.</param>
    public static MemoisedResultCase OfAsync<TMetadata>(
        string name,
        string resultType,
        Func<Task<BooleanResultBase<TMetadata>>> evaluate,
        string[]? notMemoised = null) =>
        new(name, resultType, async () => await evaluate(), MetadataReadersOf<TMetadata>(), notMemoised);

    public static TheoryData<string> NamesOf(IEnumerable<MemoisedResultCase> cases) =>
        cases.Select(memoisedCase => memoisedCase.Name).ToTheoryData();

    public static MemoisedResultCase Named(IEnumerable<MemoisedResultCase> cases, string name) =>
        cases.Single(memoisedCase => memoisedCase.Name == name);

    public async Task AssertRepeatedReadsReturnTheSameInstancesAsync()
    {
        var result = await _evaluate();

        result.GetType().NameWithoutArity().ShouldBe(
            ResultType,
            $"'{Name}' no longer reaches {ResultType}; re-point the case so that class stays covered");

        result.ShouldRebuildNothingOnASecondRead(
            Readers.Concat(_metadataReaders).Where(reader => !_notMemoised.Contains(reader.Member)),
            Name);
    }

    public override string ToString() => Name;

    private static (string Member, Func<BooleanResultBase, object> Read)[] MetadataReadersOf<TMetadata>() =>
    [
        ("MetadataTier", r => ((BooleanResultBase<TMetadata>)r).MetadataTier),
        ("MetadataTier.Metadata", r => ((BooleanResultBase<TMetadata>)r).MetadataTier.Metadata),
        ("MetadataTier.Underlying", r => ((BooleanResultBase<TMetadata>)r).MetadataTier.Underlying),
        ("Values", r => ((BooleanResultBase<TMetadata>)r).Values),
    ];
}
