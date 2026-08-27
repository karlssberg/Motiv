namespace Motiv.Traversal;

/// <summary>
/// A logical operation whose operands are evaluated by <see cref="EvaluationFold" /> rather than by the
/// operation itself, so that a composition of any depth costs heap frames instead of stack frames.
/// </summary>
/// <remarks>
/// The four members are the whole of what a driver needs that descent alone does not give it: which
/// operand comes first, whether a second one is reached, and how the outcomes compose — once for the
/// result path and once for the allocation-free boolean path that <see cref="SpecBase{TModel}.Matches" />
/// takes.
/// <para>
/// A unary operation is a binary one whose <see cref="NextOperand" /> is always <c>null</c>, which is why
/// negation implements this seam without a shape of its own.
/// </para>
/// </remarks>
/// <typeparam name="TModel">The model type the operation evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the operation's operands carry.</typeparam>
internal interface IOperationFold<TModel, TMetadata>
{
    /// <summary>The operand evaluated first. Every operation has one.</summary>
    SpecBase<TModel, TMetadata> FirstOperand { get; }

    /// <summary>
    /// The operand evaluated after <see cref="FirstOperand" />, or <c>null</c> when the operation has no
    /// second operand or when the first operand's outcome settles the result.
    /// </summary>
    /// <param name="firstSatisfied">Whether <see cref="FirstOperand" /> was satisfied.</param>
    /// <remarks>
    /// Taking the outcome as a <see cref="bool" /> rather than as a result is what lets one descent seam
    /// serve both folds: short-circuiting is a question about satisfaction, and the boolean fold has no
    /// result to hand over.
    /// </remarks>
    SpecBase<TModel, TMetadata>? NextOperand(bool firstSatisfied);

    /// <summary>Composes the operation's result from the results of the operands it reached.</summary>
    /// <param name="first">The result of <see cref="FirstOperand" />.</param>
    /// <param name="second">
    /// The result of the operand <see cref="NextOperand" /> selected, or <c>null</c> when it selected
    /// none.
    /// </param>
    BooleanResultBase<TMetadata> Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second);

    /// <summary>Composes the operation's outcome from the outcomes of the operands it reached.</summary>
    /// <param name="first">Whether <see cref="FirstOperand" /> was satisfied.</param>
    /// <param name="second">
    /// Whether the operand <see cref="NextOperand" /> selected was satisfied, or <c>null</c> when it
    /// selected none.
    /// </param>
    bool CombineMatches(bool first, bool? second);
}
