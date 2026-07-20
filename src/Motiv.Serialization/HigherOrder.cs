namespace Motiv.Serialization;

/// <summary>
/// Builds a native higher-order proposition over <c>IEnumerable&lt;TElement&gt;</c> from a bound
/// element spec, using synthesized default statements. Document decoration (name/whenTrue/whenFalse)
/// is applied afterwards by the binder's <c>Decorate</c> tail, uniformly with every other node.
/// </summary>
internal static class HigherOrder
{
    public static SpecBase<IEnumerable<TElement>, string> Build<TElement>(
        SpecBase<TElement, string> child, RuleOperator @operator, int? n) =>
        @operator switch
        {
            RuleOperator.AsAllSatisfied => Spec.Build(child).AsAllSatisfied()
                .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create(),
            RuleOperator.AsAnySatisfied => Spec.Build(child).AsAnySatisfied()
                .WhenTrue("any satisfied").WhenFalse("none satisfied").Create(),
            RuleOperator.AsNSatisfied => Spec.Build(child).AsNSatisfied(n!.Value)
                .WhenTrue($"exactly {n.Value} satisfied").WhenFalse($"not exactly {n.Value} satisfied").Create(),
            RuleOperator.AsAtLeastNSatisfied => Spec.Build(child).AsAtLeastNSatisfied(n!.Value)
                .WhenTrue($"at least {n.Value} satisfied").WhenFalse($"fewer than {n.Value} satisfied").Create(),
            RuleOperator.AsAtMostNSatisfied => Spec.Build(child).AsAtMostNSatisfied(n!.Value)
                .WhenTrue($"at most {n.Value} satisfied").WhenFalse($"more than {n.Value} satisfied").Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, "not a higher-order operator")
        };
}
