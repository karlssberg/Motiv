using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal abstract class HigherOrderResultDescriptionBase<TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TUnderlyingMetadata>[] _causes = causes.ToArray();

    internal override int CausalOperandCount => _causes.Length;

    internal override string Statement => propositionStatement;

    public override string Reason => reason;

    protected IEnumerable<string> GetUnderlyingJustificationsAsLines() =>
        _causes
            .DistinctWithOrderPreserved(result => result.Justification)
            .SelectMany(cause => cause.Description.GetJustificationAsLines());
}
