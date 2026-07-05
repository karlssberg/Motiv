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

    protected IEnumerable<string> GetUnderlyingJustificationsAsLines() =>
        DistinctCauses.SelectMany(cause => cause.Description.GetJustificationAsLines());

    protected IEnumerable<string> GetUnderlyingJustificationsWithCountsAsLines()
    {
        if (DistinctCauses.Length > 1)
            return DistinctCauses
                .SelectMany(cause => cause.Description.GetJustificationAsLines());

        return DistinctCauses
            .SelectMany(cause => cause.Description
                .GetJustificationAsLines()
                .ReplaceFirstLine(line => $"{line} ({_causes.Length})"));
    }
}
