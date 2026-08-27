namespace Motiv.Traversal;

/// <summary>
/// A policy result whose <see cref="PolicyResultBase{TMetadata}.Value" /> is not its own but the value of
/// one of the results beneath it — the operand a short-circuit stopped at, or the operand a negation
/// wraps.
/// </summary>
/// <remarks>
/// The chain of such selections is as deep as the composition, so it is walked by
/// <see cref="SelectedValue" /> rather than by each result asking the next for its <c>Value</c>. Before
/// evaluation was folded this recursion was unreachable — it needed a composition deeper than evaluation
/// itself could build.
/// </remarks>
internal interface ISelectedValueResult<TMetadata>
{
    /// <summary>The result this one takes its value from.</summary>
    PolicyResultBase<TMetadata> Selected { get; }
}
