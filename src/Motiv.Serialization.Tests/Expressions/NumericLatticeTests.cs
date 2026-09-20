#if NET8_0_OR_GREATER
using System.Linq.Expressions;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class NumericLatticeTests
{
    [Theory]
    [InlineData("Int32", "Int64", true)]
    [InlineData("Int32", "Decimal", true)]
    [InlineData("Int64", "Decimal", true)]
    [InlineData("Int32", "Double", true)]
    [InlineData("Single", "Double", true)]
    [InlineData("Int64", "Double", false)]
    [InlineData("Int32", "Single", false)]
    [InlineData("Decimal", "Double", false)]
    [InlineData("Double", "Decimal", false)]
    [InlineData("Int64", "Int32", false)]
    public void Should_allow_only_exact_widenings(string fromStr, string toStr, bool allowed)
    {
        var from = Enum.Parse<NumericKind>(fromStr);
        var to = Enum.Parse<NumericKind>(toStr);
        NumericLattice.CanWiden(from, to).ShouldBe(allowed);
    }

    [Theory]
    [InlineData("Int32", "Decimal", "Decimal")]
    [InlineData("Decimal", "Int32", "Decimal")]
    [InlineData("Int32", "Int32", "Int32")]
    [InlineData("Decimal", "Double", null)]
    [InlineData("Int64", "Double", null)]
    public void Should_join_to_the_wider_of_a_permitted_pair(string aStr, string bStr, string? expectedStr)
    {
        var a = Enum.Parse<NumericKind>(aStr);
        var b = Enum.Parse<NumericKind>(bStr);
        NumericKind? expected = expectedStr is null ? null : Enum.Parse<NumericKind>(expectedStr);
        NumericLattice.Join(a, b).ShouldBe(expected);
    }

    [Theory]
    [InlineData(typeof(int), "Int32")]
    [InlineData(typeof(long?), "Int64")]
    [InlineData(typeof(decimal), "Decimal")]
    [InlineData(typeof(float), "Single")]
    [InlineData(typeof(string), null)]
    public void Should_classify_clr_types(Type type, string? expectedStr)
    {
        NumericKind? expected = expectedStr is null ? null : Enum.Parse<NumericKind>(expectedStr);
        NumericLattice.KindOf(type).ShouldBe(expected);
    }

    [Fact]
    public void Should_parse_a_literal_exactly_as_the_target_type()
    {
        var constant = NumericLattice.Constant("0.1", NumericKind.Decimal).ShouldBeOfType<ConstantExpression>();
        constant.Value.ShouldBe(0.1m);
        constant.Type.ShouldBe(typeof(decimal));
        NumericLattice.Constant("42", NumericKind.Int64).ShouldBeOfType<ConstantExpression>().Value.ShouldBe(42L);
        Should.Throw<FormatException>(() => NumericLattice.Constant("1.5", NumericKind.Int32));
        NumericLattice.Constant("1.5", NumericKind.Single).ShouldBeOfType<ConstantExpression>().Value.ShouldBe(1.5f);
        NumericLattice.Constant("1.5", NumericKind.Double).ShouldBeOfType<ConstantExpression>().Value.ShouldBe(1.5d);
    }

    [Fact]
    public void Should_name_the_clr_type_of_every_kind()
    {
        NumericLattice.ClrType(NumericKind.Int32).ShouldBe(typeof(int));
        NumericLattice.ClrType(NumericKind.Int64).ShouldBe(typeof(long));
        NumericLattice.ClrType(NumericKind.Single).ShouldBe(typeof(float));
        NumericLattice.ClrType(NumericKind.Double).ShouldBe(typeof(double));
        NumericLattice.ClrType(NumericKind.Decimal).ShouldBe(typeof(decimal));
    }

    [Fact]
    public void Should_widen_with_a_checked_conversion_and_lift_nullables()
    {
        var value = Expression.Constant(3, typeof(int));
        var widened = NumericLattice.Widen(value, NumericKind.Decimal);
        widened.NodeType.ShouldBe(ExpressionType.ConvertChecked);
        widened.Type.ShouldBe(typeof(decimal));

        var nullable = Expression.Constant(3, typeof(int?));
        NumericLattice.Widen(nullable, NumericKind.Decimal).Type.ShouldBe(typeof(decimal?));
    }
}
#endif
