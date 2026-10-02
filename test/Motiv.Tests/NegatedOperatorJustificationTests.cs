namespace Motiv.Tests;

/// <summary>
/// Pins the operator heading that a result's <c>Justification</c> gives a negated composition. Negating
/// renames the heading (<c>AND</c> becomes <c>NAND</c>), and negating again must rename it back. Async
/// results negate through the same result description, so the sync surface covers both.
/// </summary>
public class NegatedOperatorJustificationTests
{
    private static PolicyBase<int, string> Policy(string name) =>
        Spec.Build((int n) => n > 0).Create(name);

    private static BooleanResultBase<string> Compose(string operation)
    {
        BooleanResultBase<string> left = Policy("a").Evaluate(1);
        BooleanResultBase<string> right = Policy("b").Evaluate(1);

        return operation switch
        {
            Operator.And => left.And(right),
            Operator.AndAlso => left.AndAlso(right),
            Operator.Or => left.Or(right),
            Operator.OrElse => left.OrElse(right),
            Operator.XOr => left.XOr(right),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    [Theory]
    [InlineData(Operator.And, "NAND")]
    [InlineData(Operator.AndAlso, "NAND ALSO")]
    [InlineData(Operator.Or, "NOR")]
    [InlineData(Operator.OrElse, "NOR ELSE")]
    [InlineData(Operator.XOr, "XNOR")]
    public void Should_negate_the_operator_heading_of_a_negated_result(string operation, string expected)
    {
        var sut = Compose(operation).Not();

        sut.Justification.ShouldStartWith(expected + Environment.NewLine);
    }

    [Theory]
    [InlineData(Operator.And)]
    [InlineData(Operator.AndAlso)]
    [InlineData(Operator.Or)]
    [InlineData(Operator.OrElse)]
    [InlineData(Operator.XOr)]
    public void Should_restore_the_operator_heading_of_a_doubly_negated_result(string operation)
    {
        var sut = Compose(operation).Not().Not();

        sut.Justification.ShouldStartWith(operation + Environment.NewLine);
    }
}
