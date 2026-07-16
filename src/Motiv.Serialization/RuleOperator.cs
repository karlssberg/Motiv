namespace Motiv.Serialization;

internal enum RuleOperator
{
    Spec,
    Expression,
    And,
    Or,
    XOr,
    AndAlso,
    OrElse,
    Not,
    AsAllSatisfied,
    AsAnySatisfied,
    AsNSatisfied,
    AsAtLeastNSatisfied,
    AsAtMostNSatisfied
}

internal static class RuleOperatorExtensions
{
    public static bool IsHigherOrder(this RuleOperator @operator) =>
        @operator is RuleOperator.AsAllSatisfied
            or RuleOperator.AsAnySatisfied
            or RuleOperator.AsNSatisfied
            or RuleOperator.AsAtLeastNSatisfied
            or RuleOperator.AsAtMostNSatisfied;

    public static bool RequiresN(this RuleOperator @operator) =>
        @operator is RuleOperator.AsNSatisfied
            or RuleOperator.AsAtLeastNSatisfied
            or RuleOperator.AsAtMostNSatisfied;
}
