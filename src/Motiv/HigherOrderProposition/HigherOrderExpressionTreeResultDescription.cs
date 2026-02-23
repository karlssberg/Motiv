using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderExpressionTreeResultDescription<TUnderlyingMetadata>(
    bool satisfied,
    string reason,
    LambdaExpression expression,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : ResultDescriptionBase
{
    private readonly ICollection<BooleanResultBase<TUnderlyingMetadata>> _causes = causes.ToArray();

    internal override int CausalOperandCount => _causes.Count;

    internal override string Statement => propositionStatement;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return Reason;

        // var distinctAssertions = additionalAssertions.DistinctWithOrderPreserved().ToArray();
        // var assertionIndent = distinctAssertions.Length > 0 ? 1 : 0;
        // foreach (var assertion in distinctAssertions)
        //     yield return assertion.Indent(assertionIndent);

        yield return expression.ToAssertion(satisfied).Indent();

        foreach (var line in GetUnderlyingJustificationsAsLines())
        {
            yield return line.Indent(2);
        }
    }

    private IEnumerable<string> GetUnderlyingJustificationsAsLines() =>
        _causes
            .DistinctWithOrderPreserved(result => result.Justification)
            .SelectMany(cause => cause.Description.GetJustificationAsLines());
}

