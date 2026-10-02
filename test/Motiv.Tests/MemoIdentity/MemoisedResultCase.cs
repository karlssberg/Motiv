namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// One public-API route to a result class, and the check that the result's memoised
/// <see cref="BooleanResultBase.Description" />, <see cref="BooleanResultBase.Explanation" /> and
/// <see cref="BooleanResultBase{TMetadata}.MetadataTier" /> are the same instance on every read (#294).
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
    private readonly Func<Task<BooleanResultBase>> _evaluate;
    private readonly Func<BooleanResultBase, object> _metadataTier;

    private MemoisedResultCase(
        string name,
        string resultType,
        Func<Task<BooleanResultBase>> evaluate,
        Func<BooleanResultBase, object> metadataTier)
    {
        Name = name;
        ResultType = resultType;
        _evaluate = evaluate;
        _metadataTier = metadataTier;
    }

    /// <summary>The theory-data key; unique within a test class.</summary>
    public string Name { get; }

    /// <summary>The result class the case must reach, without its generic arity.</summary>
    public string ResultType { get; }

    public static MemoisedResultCase Of<TMetadata>(
        string name,
        string resultType,
        Func<BooleanResultBase<TMetadata>> evaluate) =>
        new(name, resultType, () => Task.FromResult<BooleanResultBase>(evaluate()), TierOf<TMetadata>);

    public static MemoisedResultCase OfAsync<TMetadata>(
        string name,
        string resultType,
        Func<Task<BooleanResultBase<TMetadata>>> evaluate) =>
        new(name, resultType, async () => await evaluate(), TierOf<TMetadata>);

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

        result.Description.ShouldBeSameAs(result.Description);
        result.Explanation.ShouldBeSameAs(result.Explanation);
        _metadataTier(result).ShouldBeSameAs(_metadataTier(result));
    }

    public override string ToString() => Name;

    private static object TierOf<TMetadata>(BooleanResultBase result) =>
        ((BooleanResultBase<TMetadata>)result).MetadataTier;
}
