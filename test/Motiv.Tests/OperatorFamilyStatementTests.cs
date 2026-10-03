namespace Motiv.Tests;

/// <summary>
/// Pins how each binary operator writes a nested operand into its <c>Description.Statement</c>. An
/// operand from the same operator family is written bare (<c>a || b || c</c>); any other binary operand
/// is parenthesised (<c>(a &amp; b) || c</c>). Every operator class carries its own family list, and
/// mutation testing (#294) showed that no test read the statement of a nested composition for most of
/// them — any entry could be dropped from a list, or the check inverted, with the suite still green.
/// </summary>
/// <remarks>
/// Each case nests an operand type the operator can actually receive, and each family list names only
/// such types. A list once also named types that could never arrive — an <c>OrElsePolicy</c> is only
/// ever given policies, so an <c>OrSpec</c> entry never matched — and since no statement test could tell
/// those entries apart, they were trimmed rather than tested. Before adding an entry, check that some
/// public composition can deliver that operand type: a policy combinator receives only policies (sync
/// ones lifted through the async adapter included), and an expression-tree combinator receives only
/// expression-backed operands.
/// </remarks>
public class OperatorFamilyStatementTests
{
    private static PolicyBase<int, string> Policy(string name) =>
        Spec.Build((int n) => n > 0).Create(name);

    private static AsyncPolicyBase<int, string> AsyncPolicy(string name) =>
        Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create(name);

    private static ExpressionPolicyBase<int, string> IsPositive() =>
        Spec.From((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    private static ExpressionPolicyBase<int, string> IsSmall() =>
        Spec.From((int n) => n < 100).WhenTrue("is small").WhenFalse("is not small").Create();

    [Fact]
    public void Should_write_an_or_else_policy_operand_of_an_or_else_policy_bare()
    {
        var sut = Policy("a").OrElse(Policy("b")).OrElse(Policy("c"));

        sut.Description.Statement.ShouldBe("a || b || c");
    }

    [Fact]
    public void Should_write_an_or_operand_of_an_or_else_spec_bare()
    {
        var sut = Policy("a").Or(Policy("b")).OrElse(Policy("c"));

        sut.Description.Statement.ShouldBe("a | b || c");
    }

    [Fact]
    public void Should_write_an_and_also_policy_operand_of_an_and_also_policy_bare()
    {
        var sut = Policy("a").AndAlso(Policy("b")).AndAlso(Policy("c"));

        sut.Description.Statement.ShouldBe("a && b && c");
    }

    [Fact]
    public void Should_write_an_and_operand_of_an_and_also_spec_bare()
    {
        var sut = Policy("a").And(Policy("b")).AndAlso(Policy("c"));

        sut.Description.Statement.ShouldBe("a & b && c");
    }

    [Fact]
    public void Should_write_an_xor_operand_of_an_xor_spec_bare()
    {
        var sut = Policy("a").XOr(Policy("b")).XOr(Policy("c"));

        sut.Description.Statement.ShouldBe("a ^ b ^ c");
    }

    [Fact]
    public void Should_parenthesise_an_operand_from_another_operator_family()
    {
        var sut = Policy("a").And(Policy("b")).OrElse(Policy("c"));

        sut.Description.Statement.ShouldBe("(a & b) || c");
    }

    [Fact]
    public void Should_write_an_expression_or_else_policy_operand_of_an_expression_or_else_policy_bare()
    {
        var sut = IsPositive().OrElse(IsSmall()).OrElse(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 || n < 100 || n > 0");
    }

    [Fact]
    public void Should_write_an_expression_or_operand_of_an_expression_or_else_spec_bare()
    {
        var sut = IsPositive().Or(IsSmall()).OrElse(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 | n < 100 || n > 0");
    }

    [Fact]
    public void Should_write_an_expression_or_operand_of_an_expression_or_spec_bare()
    {
        var sut = IsPositive().Or(IsSmall()).Or(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 | n < 100 | n > 0");
    }

    [Fact]
    public void Should_write_an_expression_and_also_policy_operand_of_an_expression_and_also_policy_bare()
    {
        var sut = IsPositive().AndAlso(IsSmall()).AndAlso(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 && n < 100 && n > 0");
    }

    [Fact]
    public void Should_write_an_expression_and_operand_of_an_expression_and_also_spec_bare()
    {
        var sut = IsPositive().And(IsSmall()).AndAlso(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 & n < 100 && n > 0");
    }

    [Fact]
    public void Should_write_an_expression_and_operand_of_an_expression_and_spec_bare()
    {
        var sut = IsPositive().And(IsSmall()).And(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 & n < 100 & n > 0");
    }

    [Fact]
    public void Should_write_an_expression_xor_operand_of_an_expression_xor_spec_bare()
    {
        var sut = IsPositive().XOr(IsSmall()).XOr(IsPositive());

        sut.Description.Statement.ShouldBe("n > 0 ^ n < 100 ^ n > 0");
    }

    [Fact]
    public void Should_write_an_async_or_else_policy_operand_of_an_async_or_else_policy_bare()
    {
        var sut = AsyncPolicy("a").OrElse(AsyncPolicy("b")).OrElse(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a || b || c");
    }

    [Fact]
    public void Should_write_an_async_or_operand_of_an_async_or_else_spec_bare()
    {
        var sut = AsyncPolicy("a").Or(AsyncPolicy("b")).OrElse(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a | b || c");
    }

    [Fact]
    public void Should_write_an_async_and_also_policy_operand_of_an_async_and_also_policy_bare()
    {
        var sut = AsyncPolicy("a").AndAlso(AsyncPolicy("b")).AndAlso(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a && b && c");
    }

    [Fact]
    public void Should_write_an_async_and_operand_of_an_async_and_also_spec_bare()
    {
        var sut = AsyncPolicy("a").And(AsyncPolicy("b")).AndAlso(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a & b && c");
    }

    [Fact]
    public void Should_write_an_async_xor_operand_of_an_async_xor_spec_bare()
    {
        var sut = AsyncPolicy("a").XOr(AsyncPolicy("b")).XOr(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a ^ b ^ c");
    }

    [Fact]
    public void Should_write_an_expression_or_else_policy_operand_of_an_or_else_policy_bare()
    {
        var sut = IsPositive().OrElse(IsSmall()).OrElse(Policy("c"));

        sut.Description.Statement.ShouldBe("n > 0 || n < 100 || c");
    }

    [Fact]
    public void Should_write_an_expression_and_also_policy_operand_of_an_and_also_policy_bare()
    {
        var sut = IsPositive().AndAlso(IsSmall()).AndAlso(Policy("c"));

        sut.Description.Statement.ShouldBe("n > 0 && n < 100 && c");
    }

    [Fact]
    public void Should_write_a_lifted_or_else_policy_operand_of_an_async_or_else_policy_bare()
    {
        var sut = Policy("a").OrElse(Policy("b")).ToAsyncSpec().OrElse(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a || b || c");
    }

    [Fact]
    public void Should_write_a_lifted_expression_or_else_policy_operand_of_an_async_or_else_policy_bare()
    {
        var sut = IsPositive().OrElse(IsSmall()).ToAsyncSpec().OrElse(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("n > 0 || n < 100 || c");
    }

    [Fact]
    public void Should_write_a_lifted_and_also_policy_operand_of_an_async_and_also_policy_bare()
    {
        var sut = Policy("a").AndAlso(Policy("b")).ToAsyncSpec().AndAlso(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("a && b && c");
    }

    [Fact]
    public void Should_write_a_lifted_expression_and_also_policy_operand_of_an_async_and_also_policy_bare()
    {
        var sut = IsPositive().AndAlso(IsSmall()).ToAsyncSpec().AndAlso(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("n > 0 && n < 100 && c");
    }

    [Fact]
    public void Should_parenthesise_an_async_operand_from_another_operator_family()
    {
        var sut = AsyncPolicy("a").And(AsyncPolicy("b")).OrElse(AsyncPolicy("c"));

        sut.Description.Statement.ShouldBe("(a & b) || c");
    }
}
