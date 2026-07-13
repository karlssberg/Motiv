namespace Motiv.BooleanPredicateProposition;

internal sealed class AsyncMinimalProposition<TModel>(
    Func<TModel, CancellationToken, Task<bool>> predicate,
    ISpecDescription specDescription)
    : AsyncPolicyBase<TModel, string>
{
    private readonly string _trueBecause = GetTrueAssertion(specDescription.Statement);
    private readonly string _falseBecause = GetFalseAssertion(specDescription.Statement);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await predicate(model, cancellationToken).ConfigureAwait(false);

    protected override async Task<PolicyResultBase<string>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var isSatisfied = await predicate(model, cancellationToken).ConfigureAwait(false);
        var assertion = isSatisfied
            ? _trueBecause
            : _falseBecause;

        return new MinimalPropositionPolicyResult(
            isSatisfied,
            assertion,
            new PropositionResultDescription(assertion, Description.Statement));
    }

    private static string GetTrueAssertion(string proposition) =>
        proposition.ContainsReservedCharacters()
            ? $"({proposition})".AsSatisfied()
            : proposition.AsSatisfied();

    private static string GetFalseAssertion(string proposition) =>
        proposition.ContainsReservedCharacters()
            ? $"({proposition})".AsUnsatisfied()
            : proposition.AsUnsatisfied();
}
