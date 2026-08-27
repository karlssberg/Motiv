using Motiv.And;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.AndAlso;

/// <summary>
/// An asynchronous policy that represents the conditional AND of two asynchronous policies, preserving the
/// policy guarantee. The right operand is only evaluated if the left operand resolves to <c>true</c> — for
/// asynchronous policies this means the right operand's work (including any I/O) is never started when the
/// left operand is unsatisfied.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class AsyncAndAlsoPolicy<TModel, TMetadata>(
    AsyncPolicyBase<TModel, TMetadata> left,
    AsyncPolicyBase<TModel, TMetadata> right)
    : AsyncPolicyBase<TModel, TMetadata>,
        IAsyncBinaryOperationSpec<TModel, TMetadata>,
        IAsyncOperationFold<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [left, right];

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AsyncAndAlsoPolicy<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>
                or ExpressionAndAlsoPolicy<TModel, TMetadata>);

    /// <inheritdoc />
    public string Operation => Operator.AndAlso;

    /// <inheritdoc />
    public bool IsCollapsable => true;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Left => left;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    /// <inheritdoc />
    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        AsyncEvaluationFold.MatchesAsync(this, model, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        AsyncEvaluationFold.EvaluatePolicyAsync(this, model, cancellationToken);

    AsyncSpecBase<TModel, TMetadata> IAsyncOperationFold<TModel, TMetadata>.FirstOperand => left;

    AsyncSpecBase<TModel, TMetadata>? IAsyncOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        firstSatisfied ? right : null;

    BooleanResultBase<TMetadata> IAsyncOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        new AndAlsoPolicyResult<TMetadata>((PolicyResultBase<TMetadata>)first, (PolicyResultBase<TMetadata>?)second);

    bool IAsyncOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => second ?? first;

    bool IAsyncOperationFold<TModel, TMetadata>.IsConcurrent => false;
}
