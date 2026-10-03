namespace Motiv.Tests;

/// <summary>
/// Pins the operator heading that <c>Description.Detailed</c> gives a negated composition. Negating
/// renames the heading (<c>AND</c> becomes <c>NAND</c>), and negating again must rename it back.
/// </summary>
public class NegatedOperatorDescriptionTests
{
    private static PolicyBase<int, string> Policy(string name) =>
        Spec.Build((int n) => n > 0).Create(name);

    private static AsyncPolicyBase<int, string> AsyncPolicy(string name) =>
        Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create(name);

    private static SpecBase<int, string> Compose(string operation) =>
        operation switch
        {
            Operator.And => Policy("a").And(Policy("b")),
            Operator.AndAlso => Policy("a").AndAlso(Policy("b")),
            Operator.Or => Policy("a").Or(Policy("b")),
            Operator.OrElse => Policy("a").OrElse(Policy("b")),
            Operator.XOr => Policy("a").XOr(Policy("b")),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    private static AsyncSpecBase<int, string> ComposeAsync(string operation) =>
        operation switch
        {
            Operator.And => AsyncPolicy("a").And(AsyncPolicy("b")),
            Operator.AndAlso => AsyncPolicy("a").AndAlso(AsyncPolicy("b")),
            Operator.Or => AsyncPolicy("a").Or(AsyncPolicy("b")),
            Operator.OrElse => AsyncPolicy("a").OrElse(AsyncPolicy("b")),
            Operator.XOr => AsyncPolicy("a").XOr(AsyncPolicy("b")),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    [Theory]
    [InlineData(Operator.And, "NAND")]
    [InlineData(Operator.AndAlso, "NAND ALSO")]
    [InlineData(Operator.Or, "NOR")]
    [InlineData(Operator.OrElse, "NOR ELSE")]
    [InlineData(Operator.XOr, "XNOR")]
    public void Should_negate_the_operator_heading_of_a_negated_composition(string operation, string expected)
    {
        var sut = Compose(operation).Not();

        sut.Description.Detailed.ShouldStartWith(expected + Environment.NewLine);
    }

    [Theory]
    [InlineData(Operator.And)]
    [InlineData(Operator.AndAlso)]
    [InlineData(Operator.Or)]
    [InlineData(Operator.OrElse)]
    [InlineData(Operator.XOr)]
    public void Should_restore_the_operator_heading_of_a_doubly_negated_composition(string operation)
    {
        var sut = Compose(operation).Not().Not();

        sut.Description.Detailed.ShouldStartWith(operation + Environment.NewLine);
    }

    [Theory]
    [InlineData(Operator.And, "NAND")]
    [InlineData(Operator.AndAlso, "NAND ALSO")]
    [InlineData(Operator.Or, "NOR")]
    [InlineData(Operator.OrElse, "NOR ELSE")]
    [InlineData(Operator.XOr, "XNOR")]
    public void Should_negate_the_operator_heading_of_a_negated_async_composition(string operation, string expected)
    {
        var sut = ComposeAsync(operation).Not();

        sut.Description.Detailed.ShouldStartWith(expected + Environment.NewLine);
    }

    [Theory]
    [InlineData(Operator.And)]
    [InlineData(Operator.AndAlso)]
    [InlineData(Operator.Or)]
    [InlineData(Operator.OrElse)]
    [InlineData(Operator.XOr)]
    public void Should_restore_the_operator_heading_of_a_doubly_negated_async_composition(string operation)
    {
        var sut = ComposeAsync(operation).Not().Not();

        sut.Description.Detailed.ShouldStartWith(operation + Environment.NewLine);
    }
}
