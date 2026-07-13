using Motiv.And;
using Motiv.AndAlso;
using Motiv.MetadataToExplanationAdapter;

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

    /// <summary>Serializes the logical hierarchy of the specification to a string.</summary>
    /// <returns>A string that represents the logical hierarchy of the specification.</returns>
    public override string ToString() => Description.Statement;
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
    public new Task<BooleanResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
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
}
