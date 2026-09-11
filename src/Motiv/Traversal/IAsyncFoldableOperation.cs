namespace Motiv.Traversal;

/// <summary>
/// The asynchronous counterpart of <see cref="IFoldableOperation{TModel,TMetadata}" />: a logical
/// operation <see cref="AsyncEvaluationFold" /> can fold, whether it evaluates its operands in order or
/// all at once. See <see cref="IsConcurrent" />.
/// </summary>
/// <remarks>
/// Awaiting a synchronously-completing <see cref="ValueTask{TResult}" /> resumes on the same stack, so
/// async evaluation recursed exactly as the synchronous one did — and, with state-machine frames being
/// much fatter, failed twenty times sooner. Same seam, same driver shape, different leaf call.
/// </remarks>
/// <typeparam name="TModel">The model type the operation evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the operation's operands carry.</typeparam>
internal interface IAsyncFoldableOperation<TModel, TMetadata>
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
    /// Whether this operation evaluates its operands concurrently, in which case the driver folds it
    /// as a region rather than as a frame.
    /// </summary>
    /// <remarks>
    /// Concurrency is a fan-out rather than a walk, so the two are folded by different loops: a frame's
    /// operands are ordered and a concurrent operation's are not. The driver absorbs an unbroken run of
    /// concurrent operations into one region and starts every operand at its boundary together
    /// (<see href="https://github.com/karlssberg/Motiv/issues/145">#145</see>). Until then the driver
    /// left such a node to evaluate itself, which recursed once per layer.
    /// <para>
    /// <b>The flag also says <em>eager</em>, and the region walk depends on it.</b> A concurrent
    /// operation starts both operands regardless of outcome, so <see cref="NextOperand" /> returns the
    /// same operand whichever outcome it is told — which is what lets the region's shape be read off the
    /// composition without evaluating anything.
    /// </para>
    /// </remarks>
    bool IsConcurrent { get; }
}
