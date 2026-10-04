using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

/// <summary>
/// Expression shapes <see cref="CSharpExpressionSerializer"/> used to print as text that reads as a
/// different expression, or as no expression at all. Each test pins the C# an author would have written.
/// </summary>
public class CSharpExpressionSerializerFidelityTests
{
    public class Point
    {
        public int X { get; set; }
    }

    public class Shape
    {
        public Point Origin { get; } = new();
        public int this[int index] => index;
    }

    private static string Body<T>(Expression<T> expression) => expression.Body.Serialize();

    [Fact]
    public void Should_qualify_a_static_member_with_its_type()
    {
        Body<Func<DateTime>>(() => DateTime.Now).ShouldBe("DateTime.Now");
        Body<Func<string, bool>>(s => s == string.Empty).ShouldBe("s == string.Empty");
    }

    [Fact]
    public void Should_parenthesise_the_operand_of_a_narrowing_cast()
    {
        Body<Func<long, int>>(l => (int)(l + 1)).ShouldBe("(int)(l + 1)");
        Body<Func<long, int>>(l => checked((int)(l + 1))).ShouldBe("checked((int)checked(l + 1))");
        Body<Func<int, int, int>>((a, b) => checked(a + b) * 2).ShouldBe("checked(a + b) * 2");
    }

    [Fact]
    public void Should_not_merge_nested_signs_into_a_decrement_or_increment()
    {
        var n = Expression.Parameter(typeof(int), "n");

        Body<Func<int, int>>(n => -(-n)).ShouldBe("-(-n)");
        Expression.UnaryPlus(Expression.UnaryPlus(n)).Serialize().ShouldBe("+(+n)");
        Expression.Negate(Expression.Constant(-1)).Serialize().ShouldBe("-(-1)");
        Body<Func<int, int>>(n => -~n).ShouldBe("-~n");
    }

    [Fact]
    public void Should_print_a_nested_member_binding_with_one_pair_of_braces()
    {
        Body<Func<int, Shape>>(n => new Shape { Origin = { X = n } }).ShouldBe("new Shape { Origin = { X = n } }");
    }

    [Fact]
    public void Should_print_an_index_expression_on_its_object_with_its_arguments()
    {
        var array = Expression.Parameter(typeof(int[]), "a");
        var shape = Expression.Parameter(typeof(Shape), "s");
        var i = Expression.Parameter(typeof(int), "i");

        Expression.ArrayAccess(array, i).Serialize().ShouldBe("a[i]");
        Expression.Property(shape, "Item", i).Serialize().ShouldBe("s[i]");
    }

    [Fact]
    public void Should_print_an_unbox_as_a_cast()
    {
        var o = Expression.Parameter(typeof(object), "o");

        Expression.Unbox(o, typeof(int)).Serialize().ShouldBe("(int)o");
    }

    [Fact]
    public void Should_print_a_guid_constant_as_a_quoted_parse()
    {
        Expression.Constant(new Guid("01234567-89ab-cdef-0123-456789abcdef")).Serialize()
            .ShouldBe("Guid.Parse(\"01234567-89ab-cdef-0123-456789abcdef\")");
    }
}
