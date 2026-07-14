using Motiv.And;
using Motiv.AndAlso;
using Motiv.Diagnostics;
using Motiv.MetadataToExplanationAdapter;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.XOr;

namespace Motiv;

/// <summary>
/// The base class for asynchronous specifications. Mirrors <see cref="SpecBase{TModel}" /> but evaluates
/// asynchronously via <see cref="EvaluateAsync" /> and <see cref="MatchesAsync" />. Async specifications
/// share the non-generic <see cref="SpecBase" /> root so that names, descriptions, and underlying-spec
/// traversal behave identically to their synchronous counterparts.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
public abstract class AsyncSpecBase<TModel> : SpecBase
{
    /// <summary>Prevents the external instantiation of the <see cref="AsyncSpecBase{TModel}" /> class.</summary>
    internal AsyncSpecBase()
    {
    }

    /// <summary>
    /// Asynchronously evaluates the proposition against the model and returns a boolean indicating whether it
    /// is satisfied, without allocating result objects where the underlying propositions permit.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns><c>true</c> if the model satisfies the proposition; otherwise, <c>false</c>.</returns>
    public virtual async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        (await EvaluateAsync(model, cancellationToken).ConfigureAwait(false)).Satisfied;

    /// <summary>
    /// Asynchronously evaluates the proposition against the model and returns a result that contains the
    /// Boolean result of the predicate and an explanation of the result.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate and an explanation of the result.</returns>
    public Task<BooleanResultBase<string>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
        this switch
        {
            AsyncSpecBase<TModel, string> explanationSpec => explanationSpec.EvaluateAsync(model, cancellationToken),
            _ => ToAsyncExplanationSpec().EvaluateAsync(model, cancellationToken)
        };

    /// <summary>
    /// Converts this specification to an asynchronous explanation specification (i.e.,
    /// AsyncSpecBase&lt;TModel, string&gt;). This is necessary when establishing a
    /// "lowest-common-denominator" between very different specifications.
    /// </summary>
    /// <returns>An equivalent specification that uses assertions as its metadata.</returns>
    public abstract AsyncSpecBase<TModel, string> ToAsyncExplanationSpec();

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical AND operator. Both
    /// operands are lowered to their explanation form via <see cref="ToAsyncExplanationSpec" /> so that
    /// specifications with differing metadata types can be composed. Both operands will be evaluated, regardless
    /// of whether the left operand evaluated to <c>false</c>.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> And(AsyncSpecBase<TModel> spec) =>
        new AsyncAndSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToAsyncExplanationSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical AND operator. Both
    /// operands are lowered to their explanation form; the synchronous operand is then lifted into the
    /// asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />. Both operands will be
    /// evaluated, regardless of whether the left operand evaluated to <c>false</c>.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> And(SpecBase<TModel> spec) =>
        new AsyncAndSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToExplanationSpec().ToAsyncSpec());

    /// <summary>
    /// Combines this specification with another asynchronous specification using the conditional AND operator.
    /// Both operands are lowered to their explanation form via <see cref="ToAsyncExplanationSpec" /> so that
    /// specifications with differing metadata types can be composed. The right operand is only evaluated if the
    /// left operand evaluates to <c>true</c>. This is commonly referred to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> AndAlso(AsyncSpecBase<TModel> spec) =>
        new AsyncAndAlsoSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToAsyncExplanationSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the conditional AND operator. Both
    /// operands are lowered to their explanation form; the synchronous operand is then lifted into the
    /// asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />. The right operand is
    /// only evaluated if the left operand evaluates to <c>true</c>. This is commonly referred to as
    /// "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> AndAlso(SpecBase<TModel> spec) =>
        new AsyncAndAlsoSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToExplanationSpec().ToAsyncSpec());

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical OR operator. Both
    /// operands are lowered to their explanation form via <see cref="ToAsyncExplanationSpec" /> so that
    /// specifications with differing metadata types can be composed.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> Or(AsyncSpecBase<TModel> spec) =>
        new AsyncOrSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToAsyncExplanationSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical OR operator. Both operands
    /// are lowered to their explanation form; the synchronous operand is then lifted into the asynchronous
    /// hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> Or(SpecBase<TModel> spec) =>
        new AsyncOrSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToExplanationSpec().ToAsyncSpec());

    /// <summary>
    /// Combines this specification with another asynchronous specification using the conditional OR operator.
    /// Both operands are lowered to their explanation form via <see cref="ToAsyncExplanationSpec" /> so that
    /// specifications with differing metadata types can be composed. The right operand is only evaluated if the
    /// left operand evaluates to <c>false</c>. This is commonly referred to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> OrElse(AsyncSpecBase<TModel> spec) =>
        new AsyncOrElseSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToAsyncExplanationSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the conditional OR operator. Both
    /// operands are lowered to their explanation form; the synchronous operand is then lifted into the
    /// asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />. The right operand is
    /// only evaluated if the left operand evaluates to <c>false</c>. This is commonly referred to as
    /// "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> OrElse(SpecBase<TModel> spec) =>
        new AsyncOrElseSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToExplanationSpec().ToAsyncSpec());

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical XOR operator. Both
    /// operands are lowered to their explanation form via <see cref="ToAsyncExplanationSpec" /> so that
    /// specifications with differing metadata types can be composed.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> XOr(AsyncSpecBase<TModel> spec) =>
        new AsyncXOrSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToAsyncExplanationSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical XOR operator. Both
    /// operands are lowered to their explanation form; the synchronous operand is then lifted into the
    /// asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, string> XOr(SpecBase<TModel> spec) =>
        new AsyncXOrSpec<TModel, string>(ToAsyncExplanationSpec(), spec.ToExplanationSpec().ToAsyncSpec());

    /// <summary>Negates this specification.</summary>
    /// <returns>A new specification that represents the logical NOT of this specification.</returns>
    public AsyncSpecBase<TModel, string> Not() =>
        new AsyncNotSpec<TModel, string>(ToAsyncExplanationSpec());

    /// <summary>Serializes the logical hierarchy of the specification to a string.</summary>
    /// <returns>A string that represents the logical hierarchy of the specification.</returns>
    public override string ToString() => Description.Statement;

    /// <summary>Combines two asynchronous specifications using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator &(
        AsyncSpecBase<TModel> left,
        AsyncSpecBase<TModel> right) =>
        left.And(right);

    /// <summary>Combines an asynchronous specification with a synchronous specification using the logical AND operator.</summary>
    /// <param name="left">The asynchronous left operand of the AND operation.</param>
    /// <param name="right">The synchronous right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator &(
        AsyncSpecBase<TModel> left,
        SpecBase<TModel> right) =>
        left.And(right);

    /// <summary>Combines a synchronous specification with an asynchronous specification using the logical AND operator.</summary>
    /// <param name="left">The synchronous left operand of the AND operation.</param>
    /// <param name="right">The asynchronous right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator &(
        SpecBase<TModel> left,
        AsyncSpecBase<TModel> right) =>
        new AsyncAndSpec<TModel, string>(left.ToExplanationSpec().ToAsyncSpec(), right.ToAsyncExplanationSpec());

    /// <summary>Combines two asynchronous specifications using the logical OR operator.</summary>
    /// <param name="left">The left operand of the OR operation.</param>
    /// <param name="right">The right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator |(
        AsyncSpecBase<TModel> left,
        AsyncSpecBase<TModel> right) =>
        left.Or(right);

    /// <summary>Combines an asynchronous specification with a synchronous specification using the logical OR operator.</summary>
    /// <param name="left">The asynchronous left operand of the OR operation.</param>
    /// <param name="right">The synchronous right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator |(
        AsyncSpecBase<TModel> left,
        SpecBase<TModel> right) =>
        left.Or(right);

    /// <summary>Combines a synchronous specification with an asynchronous specification using the logical OR operator.</summary>
    /// <param name="left">The synchronous left operand of the OR operation.</param>
    /// <param name="right">The asynchronous right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator |(
        SpecBase<TModel> left,
        AsyncSpecBase<TModel> right) =>
        new AsyncOrSpec<TModel, string>(left.ToExplanationSpec().ToAsyncSpec(), right.ToAsyncExplanationSpec());

    /// <summary>Combines two asynchronous specifications using the logical XOR operator.</summary>
    /// <param name="left">The left operand of the XOR operation.</param>
    /// <param name="right">The right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator ^(
        AsyncSpecBase<TModel> left,
        AsyncSpecBase<TModel> right) =>
        left.XOr(right);

    /// <summary>Combines an asynchronous specification with a synchronous specification using the logical XOR operator.</summary>
    /// <param name="left">The asynchronous left operand of the XOR operation.</param>
    /// <param name="right">The synchronous right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator ^(
        AsyncSpecBase<TModel> left,
        SpecBase<TModel> right) =>
        left.XOr(right);

    /// <summary>Combines a synchronous specification with an asynchronous specification using the logical XOR operator.</summary>
    /// <param name="left">The synchronous left operand of the XOR operation.</param>
    /// <param name="right">The asynchronous right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, string> operator ^(
        SpecBase<TModel> left,
        AsyncSpecBase<TModel> right) =>
        new AsyncXOrSpec<TModel, string>(left.ToExplanationSpec().ToAsyncSpec(), right.ToAsyncExplanationSpec());

    /// <summary>Negates a specification.</summary>
    /// <param name="spec">The specification to negate.</param>
    /// <returns>A new specification that represents the logical NOT of the specification.</returns>
    public static AsyncSpecBase<TModel> operator !(
        AsyncSpecBase<TModel> spec) =>
        spec.Not();
}

/// <summary>
/// The base class for asynchronous specifications that yield metadata. Mirrors
/// <see cref="SpecBase{TModel, TMetadata}" /> for asynchronous evaluation. Results are the same immutable
/// <see cref="BooleanResultBase{TMetadata}" /> instances produced by synchronous specifications.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate</typeparam>
public abstract class AsyncSpecBase<TModel, TMetadata> : AsyncSpecBase<TModel>
{
    private AsyncSpecBase<TModel, string>? _explanationSpec;

    /// <summary>Prevents the external instantiation of the <see cref="AsyncSpecBase{TModel,TMetadata}" /> class.</summary>
    internal AsyncSpecBase()
    {
    }

    /// <inheritdoc />
    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        (await EvaluateSpecAsync(model, cancellationToken).ConfigureAwait(false)).Satisfied;

    /// <summary>
    /// Asynchronously evaluates the proposition against the model and returns a result that contains the
    /// Boolean result of the predicate in addition to the metadata.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    public new Task<BooleanResultBase<TMetadata>> EvaluateAsync(
        TModel model,
        CancellationToken cancellationToken = default) =>
        MotivTelemetry.IsEnabled
            ? EvaluateSpecInstrumentedAsync(model, cancellationToken)
            : EvaluateSpecAsync(model, cancellationToken);

    /// <summary>
    /// Kept separate from <see cref="EvaluateAsync(TModel,CancellationToken)" /> so that the public boundary
    /// remains free of an async state machine when telemetry is disabled.
    /// </summary>
    private async Task<BooleanResultBase<TMetadata>> EvaluateSpecInstrumentedAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var scope = EvaluationScope.Start(Description.Statement);
        BooleanResultBase<TMetadata> result;
        try
        {
            result = await EvaluateSpecAsync(model, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller's own token was signalled, so this cancellation is intentional, not a failure — see
            // EvaluationScope.Cancel. An OperationCanceledException without the caller's token signalled (an
            // internal timeout, a foreign token, a bug) falls through to the catch below and is still an error.
            scope.Cancel();
            throw;
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
            throw;
        }

        try
        {
            scope.Complete(result);
        }
        catch
        {
            // A listener or exporter failing while consuming the completed scope (e.g. a throwing
            // ActivityStopped callback invoked synchronously by Activity.Dispose()) belongs to telemetry, not
            // the evaluation: this call already succeeded, so that failure must not be attributed to it as an
            // error, nor allowed to escape — deliberately not scope.Fail(...).
        }

        return result;
    }

    /// <summary>
    /// Asynchronously evaluates the specification without emitting telemetry. Used by composite and adapter
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its
    /// root rather than one per node.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    internal Task<BooleanResultBase<TMetadata>> EvaluateSpecAsyncInternal(
        TModel model,
        CancellationToken cancellationToken) =>
        EvaluateSpecAsync(model, cancellationToken);

    /// <inheritdoc />
    public override AsyncSpecBase<TModel, string> ToAsyncExplanationSpec() =>
        this switch
        {
            AsyncSpecBase<TModel, string> explanationSpec => explanationSpec,
            _ => _explanationSpec ??= new AsyncMetadataToExplanationAdapterSpec<TModel, TMetadata>(this)
        };

    /// <summary>
    /// Asynchronously evaluates the specification against the model and returns a result that contains the
    /// Boolean result of the predicate in addition to the metadata.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    protected abstract Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(TModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical AND operator.
    /// Both operands are evaluated sequentially (left, then right), regardless of the left operand's outcome.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> And(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncAndSpec<TModel, TMetadata>(this, spec);

    /// <summary>
    /// Combines this specification with another asynchronous specification using the conditional AND
    /// operator. The right operand is only evaluated if the left operand resolves to <c>true</c> — for
    /// asynchronous specifications this means the right operand's work (including any I/O) is never started.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> AndAlso(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncAndAlsoSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines two asynchronous specifications using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator &(
        AsyncSpecBase<TModel, TMetadata> left,
        AsyncSpecBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <summary>Combines this specification with another specification using the logical OR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> Or(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncOrSpec<TModel, TMetadata>(this, spec);

    /// <summary>
    /// Combines this specification with another specification using the conditional OR operator. The right
    /// operand is only evaluated if the left operand resolves to <c>false</c>, since a <c>true</c> left
    /// operand means the OR operation is already satisfied — for asynchronous specifications this means the
    /// right operand's work (including any I/O) is never started.
    /// </summary>
    /// <param name="spec">The right operand.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> OrElse(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncOrElseSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines two asynchronous specifications using the logical OR operator.</summary>
    /// <param name="left">The left operand of the OR operation.</param>
    /// <param name="right">The right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator |(
        AsyncSpecBase<TModel, TMetadata> left,
        AsyncSpecBase<TModel, TMetadata> right) =>
        left.Or(right);

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical XOR operator.
    /// Both operands are evaluated sequentially (left, then right), regardless of the outcome.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> XOr(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncXOrSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines two asynchronous specifications using the logical XOR operator.</summary>
    /// <param name="left">The left operand of the XOR operation.</param>
    /// <param name="right">The right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator ^(
        AsyncSpecBase<TModel, TMetadata> left,
        AsyncSpecBase<TModel, TMetadata> right) =>
        left.XOr(right);

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical AND operator,
    /// evaluating both operands concurrently. The result is indistinguishable from <see cref="And" /> — the
    /// reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> AndConcurrently(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncAndSpec<TModel, TMetadata>(this, spec, concurrent: true);

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical OR operator,
    /// evaluating both operands concurrently. The result is indistinguishable from <see cref="Or" /> — the
    /// reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> OrConcurrently(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncOrSpec<TModel, TMetadata>(this, spec, concurrent: true);

    /// <summary>
    /// Combines this specification with another asynchronous specification using the logical XOR operator,
    /// evaluating both operands concurrently. The result is indistinguishable from <see cref="XOr" /> — the
    /// reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> XOrConcurrently(AsyncSpecBase<TModel, TMetadata> spec) =>
        new AsyncXOrSpec<TModel, TMetadata>(this, spec, concurrent: true);

    /// <summary>Negates this specification.</summary>
    /// <returns>A new specification that represents the logical NOT of this specification.</returns>
    public new AsyncSpecBase<TModel, TMetadata> Not() =>
        new AsyncNotSpec<TModel, TMetadata>(this);

    /// <summary>Negates a specification.</summary>
    /// <param name="spec">The specification to negate.</param>
    /// <returns>A new specification that represents the logical NOT of the specification.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator !(
        AsyncSpecBase<TModel, TMetadata> spec) =>
        spec.Not();

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical AND operator. The
    /// synchronous operand is lifted into the asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// Both operands are evaluated sequentially (left, then right), regardless of the left operand's outcome.
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> And(SpecBase<TModel, TMetadata> spec) => And(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the conditional AND operator. The
    /// synchronous operand is lifted into the asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// The right operand is only evaluated if the left operand resolves to <c>true</c> — for asynchronous
    /// specifications this means the right operand's work (including any I/O) is never started.
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> AndAlso(SpecBase<TModel, TMetadata> spec) => AndAlso(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical OR operator. The
    /// synchronous operand is lifted into the asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> Or(SpecBase<TModel, TMetadata> spec) => Or(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the conditional OR operator. The
    /// synchronous operand is lifted into the asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// The right operand is only evaluated if the left operand resolves to <c>false</c>, since a <c>true</c>
    /// left operand means the OR operation is already satisfied — for asynchronous specifications this means
    /// the right operand's work (including any I/O) is never started.
    /// </summary>
    /// <param name="spec">The synchronous right operand.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> OrElse(SpecBase<TModel, TMetadata> spec) => OrElse(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical XOR operator. The
    /// synchronous operand is lifted into the asynchronous hierarchy via <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// Both operands are evaluated sequentially (left, then right), regardless of the outcome.
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> XOr(SpecBase<TModel, TMetadata> spec) => XOr(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical AND operator, evaluating
    /// both operands concurrently. The synchronous operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />. The result is indistinguishable from <see cref="And(SpecBase{TModel,TMetadata})" /> —
    /// the reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> AndConcurrently(SpecBase<TModel, TMetadata> spec) => AndConcurrently(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical OR operator, evaluating
    /// both operands concurrently. The synchronous operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />. The result is indistinguishable from <see cref="Or(SpecBase{TModel,TMetadata})" /> —
    /// the reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> OrConcurrently(SpecBase<TModel, TMetadata> spec) => OrConcurrently(spec.ToAsyncSpec());

    /// <summary>
    /// Combines this specification with a synchronous specification using the logical XOR operator, evaluating
    /// both operands concurrently. The synchronous operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />. The result is indistinguishable from <see cref="XOr(SpecBase{TModel,TMetadata})" /> —
    /// the reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The synchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> XOrConcurrently(SpecBase<TModel, TMetadata> spec) => XOrConcurrently(spec.ToAsyncSpec());

    /// <summary>
    /// Combines an asynchronous specification with a synchronous specification using the logical AND operator.
    /// The synchronous right operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="left">The asynchronous left operand of the AND operation.</param>
    /// <param name="right">The synchronous right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator &(
        AsyncSpecBase<TModel, TMetadata> left, SpecBase<TModel, TMetadata> right) => left.And(right);

    /// <summary>
    /// Combines a synchronous specification with an asynchronous specification using the logical AND operator.
    /// The synchronous left operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="left">The synchronous left operand of the AND operation.</param>
    /// <param name="right">The asynchronous right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator &(
        SpecBase<TModel, TMetadata> left, AsyncSpecBase<TModel, TMetadata> right) => left.ToAsyncSpec().And(right);

    /// <summary>
    /// Combines an asynchronous specification with a synchronous specification using the logical OR operator.
    /// The synchronous right operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="left">The asynchronous left operand of the OR operation.</param>
    /// <param name="right">The synchronous right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator |(
        AsyncSpecBase<TModel, TMetadata> left, SpecBase<TModel, TMetadata> right) => left.Or(right);

    /// <summary>
    /// Combines a synchronous specification with an asynchronous specification using the logical OR operator.
    /// The synchronous left operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="left">The synchronous left operand of the OR operation.</param>
    /// <param name="right">The asynchronous right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator |(
        SpecBase<TModel, TMetadata> left, AsyncSpecBase<TModel, TMetadata> right) => left.ToAsyncSpec().Or(right);

    /// <summary>
    /// Combines an asynchronous specification with a synchronous specification using the logical XOR operator.
    /// The synchronous right operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="left">The asynchronous left operand of the XOR operation.</param>
    /// <param name="right">The synchronous right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator ^(
        AsyncSpecBase<TModel, TMetadata> left, SpecBase<TModel, TMetadata> right) => left.XOr(right);

    /// <summary>
    /// Combines a synchronous specification with an asynchronous specification using the logical XOR operator.
    /// The synchronous left operand is lifted into the asynchronous hierarchy via
    /// <see cref="SpecBase{TModel,TMetadata}.ToAsyncSpec" />.
    /// </summary>
    /// <param name="left">The synchronous left operand of the XOR operation.</param>
    /// <param name="right">The asynchronous right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static AsyncSpecBase<TModel, TMetadata> operator ^(
        SpecBase<TModel, TMetadata> left, AsyncSpecBase<TModel, TMetadata> right) => left.ToAsyncSpec().XOr(right);
}
