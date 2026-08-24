using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal abstract class HigherOrderResultDescriptionBase<TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TUnderlyingMetadata>[] _causes =
        causes as BooleanResultBase<TUnderlyingMetadata>[] ?? causes.ToArray();

    internal override int CausalOperandCount => _causes.Length;

    internal override string Statement => propositionStatement;

    public override string Reason => reason;

    private BooleanResultBase<TUnderlyingMetadata>[] DistinctCauses =>
        field ??= _causes
            .DistinctWithOrderPreserved(result => result.Justification)
            .ToArray();

    /// <remarks>
    /// A cause's lines are always taken with the causal count, in both of this description's modes.
    /// </remarks>
    private protected override IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) =>
        Array.ConvertAll(DistinctCauses, cause => new Rendering(cause.Description, false));

    private protected static IEnumerable<string> UnderlyingJustifications(IReadOnlyList<string[]> causeLines) =>
        Concatenated(causeLines);

    /// <summary>
    /// As <see cref="UnderlyingJustifications" />, except that a lone distinct cause carries the
    /// number of causes it stands for.
    /// </summary>
    private protected IEnumerable<string> UnderlyingJustificationsWithCounts(IReadOnlyList<string[]> causeLines) =>
        causeLines.Count > 1
            ? Concatenated(causeLines)
            : Concatenated(causeLines).ReplaceFirstLine(line => $"{line} ({_causes.Length})");
}
