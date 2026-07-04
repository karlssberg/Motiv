using System.Linq.Expressions;
using Motiv.Traversal;

namespace Motiv.Tests;

public class ExpressionTreeToSpecExpressionTests
{
    public record Customer(int Age, bool IsActive, List<int> OrderTotals)
    {
        public int Age { get; } = Age;
        public bool IsActive { get; } = IsActive;
        public List<int> OrderTotals { get; } = OrderTotals;
    }

    [Fact]
    public void Should_return_an_expression_spec_from_to_spec()
    {
        // Arrange
        Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;

        // Act — static typing is part of the assertion
        ExpressionSpecBase<Customer, string> act = expression.ToSpec();

        // Assert
        act.ToExpression().Compile()(new Customer(30, true, [])).ShouldBeTrue();
    }

    [Fact]
    public void Should_decompose_into_atoms_that_expose_their_source_fragments()
    {
        // Arrange
        Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;

        // Act
        var act = expression.ToSpec();

        // Assert — the root is a binary composite whose operands each carry their fragment
        var binary = act.ShouldBeAssignableTo<IBinaryOperationSpec<Customer, string>>();
        var leftFragment = binary!.Left.ShouldBeAssignableTo<IExpressionSpec<Customer>>()!.ToExpression();
        var rightFragment = binary.Right.ShouldBeAssignableTo<IExpressionSpec<Customer>>()!.ToExpression();
        leftFragment.Compile()(new Customer(17, true, [])).ShouldBeFalse();
        leftFragment.Compile()(new Customer(18, false, [])).ShouldBeTrue();
        rightFragment.Compile()(new Customer(17, true, [])).ShouldBeTrue();
    }

    [Fact]
    public void Should_keep_inline_any_decomposition_expression_backed()
    {
        // Arrange
        Expression<Func<Customer, bool>> expression = c => c.OrderTotals.Any(t => t > 100);

        // Act
        var act = expression.ToSpec();

        // Assert — explanation still flows through the higher-order decomposition
        act.ToExpression().Compile()(new Customer(30, true, [50, 150])).ShouldBeTrue();
        act.Evaluate(new Customer(30, true, [50])).Satisfied.ShouldBeFalse();
    }

    [Theory]
    [InlineData(17, true)]
    [InlineData(30, false)]
    public void Should_preserve_existing_explanations_after_transformer_change(int age, bool isMinor)
    {
        // Arrange — guard: assertions must be identical to the pre-change behaviour
        var sut = Spec.From((Customer c) => c.Age >= 18).Create("is adult");

        // Act
        var act = sut.Evaluate(new Customer(age, true, []));

        // Assert
        act.Satisfied.ShouldBe(!isMinor);
    }
}
