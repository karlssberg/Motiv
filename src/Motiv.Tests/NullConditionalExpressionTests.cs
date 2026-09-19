using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class NullConditionalExpressionTests
{
    private sealed record Order(decimal Total)
    {
        // net472 cannot compile the positional init-only property (CS0518: IsExternalInit missing);
        // the get-only override mirrors AsyncDecoratorMetadataTests.Verdict.
        public decimal Total { get; } = Total;
    }

    private sealed record Address(string? Country)
    {
        // net472 cannot compile the positional init-only property (CS0518: IsExternalInit missing);
        // the get-only override mirrors AsyncDecoratorMetadataTests.Verdict.
        public string? Country { get; } = Country;
    }

    private sealed record Customer(IReadOnlyList<Order>? Orders, string? Country, Address? Address = null)
    {
        // net472 cannot compile the positional init-only properties (CS0518: IsExternalInit missing);
        // the get-only overrides mirror AsyncDecoratorMetadataTests.Verdict.
        public IReadOnlyList<Order>? Orders { get; } = Orders;
        public string? Country { get; } = Country;
        public Address? Address { get; } = Address;
    }

    private static class Counter
    {
        public static int Count;

        public static Customer? Next()
        {
            Count++;
            return new Customer([], "SE");
        }
    }

    [Fact]
    public void Should_evaluate_to_null_when_the_receiver_is_null_and_to_the_value_otherwise()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var orders = Expression.Property(c, nameof(Customer.Orders));
        var count = NullConditionalExpression.Create(orders, target =>
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [typeof(Order)], target));
        count.Type.ShouldBe(typeof(int?));

        var lambda = Expression.Lambda<Func<Customer, int?>>(count, c).Compile();
        lambda(new Customer(null, null)).ShouldBeNull();
        lambda(new Customer([new Order(1), new Order(2)], null)).ShouldBe(2);
    }

    [Fact]
    public void Should_print_as_a_null_conditional_access()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var orders = Expression.Property(c, nameof(Customer.Orders));
        var count = NullConditionalExpression.Create(orders, target =>
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [typeof(Order)], target));
        var body = Expression.GreaterThan(count, Expression.Constant(2, typeof(int?)));

        var spec = Spec.From(Expression.Lambda<Func<Customer, bool>>(body, c)).Create("has orders");
        var result = spec.Evaluate(new Customer([new Order(1), new Order(2), new Order(3)], null));

        result.Assertions.ShouldBe(["c.Orders?.Count() > 2"]);
    }

    [Fact]
    public void Should_make_a_comparison_false_when_the_path_is_null()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var length = NullConditionalExpression.Create(Expression.Property(c, nameof(Customer.Country)), target =>
            Expression.Property(target, nameof(string.Length)));
        var body = Expression.GreaterThan(length, Expression.Constant(1, typeof(int?)));
        var spec = Spec.From(Expression.Lambda<Func<Customer, bool>>(body, c)).Create("long country");

        spec.Evaluate(new Customer(null, null)).Satisfied.ShouldBeFalse();
        spec.Evaluate(new Customer(null, "SE")).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_evaluate_the_target_exactly_once()
    {
        Counter.Count = 0;
        var call = Expression.Call(typeof(Counter).GetMethod(nameof(Counter.Next))!);
        var countryAccess = NullConditionalExpression.Create(call, target =>
            Expression.Property(target, nameof(Customer.Country)));

        var lambda = Expression.Lambda<Func<string?>>(countryAccess).Compile();
        var result = lambda();

        result.ShouldBe<string?>("SE");
        Counter.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_print_a_multi_hop_null_conditional_chain()
    {
        var c = Expression.Parameter(typeof(Customer), "c");
        var addressCountry = Expression.Property(Expression.Property(c, nameof(Customer.Address)), nameof(Address.Country));
        var length = NullConditionalExpression.Create(addressCountry, target =>
            Expression.Property(target, nameof(string.Length)));
        var body = Expression.GreaterThan(length, Expression.Constant(1, typeof(int?)));

        var spec = Spec.From(Expression.Lambda<Func<Customer, bool>>(body, c)).Create("long address country");
        var result = spec.Evaluate(new Customer(null, null, new Address("SE")));

        result.Assertions.ShouldBe(["c.Address.Country?.Length > 1"]);
    }
}
