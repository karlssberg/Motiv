namespace Motiv.Traversal;

/// <summary>
/// The asynchronous counterpart of <see cref="IOperationFold{TModel,TMetadata}" />: a logical operation
/// whose operands are evaluated by <see cref="AsyncEvaluationFold" /> rather than by the operation itself.
/// </summary>
/// <remarks>
/// Awaiting a synchronously-completing <see cref="ValueTask{TResult}" /> resumes on the same stack, so
/// async evaluation recursed exactly as the synchronous one did — and, with state-machine frames being
/// much fatter, failed twenty times sooner. Same seam, same driver shape, different leaf call.
/// </remarks>
/// <typeparam name="TModel">The model type the operation evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the operation's operands carry.</typeparam>
internal interface IAsyncOperationFold<TModel, TMetadata>
{
    /// <summary>The operand evaluated first. Every operation has one.</summary>
    AsyncSpecBase<TModel, TMetadata> FirstOperand { get; }

    /// <summary>
    /// The operand evaluated after <see cref="FirstOperand" />, or <c>null</c> when the operation has no
    /// second operand or when the first operand's outcome settles the result.
    /// </summary>
    /// <param name="firstSatisfied">Whether <see cref="FirstOperand" /> was satisfied.</param>
    AsyncSpecBase<TModel, TMetadata>? NextOperand(bool firstSatisfied);

    /// <summary>Composes the operation's result from the results of the operands it reached.</summary>
    BooleanResultBase<TMetadata> Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second);

    /// <summary>Composes the operation's outcome from the outcomes of the operands it reached.</summary>
    bool CombineMatches(bool first, bool? second);

    /// <summary>
    /// Whether this operation evaluates its operands concurrently, in which case the driver leaves it to
    /// evaluate itself.
    /// </summary>
    /// <remarks>
    /// Concurrency is a fan-out rather than a walk, and folding it would mean a genuinely parallel
    /// driver. It is also unreachable from a rule document — <c>AsyncRuleBinder</c> composes only
    /// sequential operators — so the depth it can recurse to is the depth an author writes by hand.
    /// </remarks>
    bool IsConcurrent { get; }
}
