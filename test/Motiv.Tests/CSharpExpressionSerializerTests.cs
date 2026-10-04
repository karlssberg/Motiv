using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

/// <summary>
/// The C# text <see cref="CSharpExpressionSerializer"/> writes for each expression shape, asserted exactly.
/// This text is what expression-tree propositions show as their assertions and justification lines, so a
/// shape that prints wrongly is an explanation that reads wrongly. Several shapes here had no test that
/// serialised them at all (Stryker run 2026-10-03), and the shapes C# lambdas cannot produce are built with
/// the <see cref="Expression"/> factory methods directly.
/// </summary>
public class CSharpExpressionSerializerTests
{
    public enum Colour { Red, Green }

    public class Inner
    {
        public int X { get; set; }
    }

    public class Box
    {
        public Box() { }

        public Box(int value) => Value = value;

        public int Value { get; set; }
        public string Text { get; set; } = "";
        public bool Flag { get; set; }
        public List<int> Items { get; } = [];
        public Inner Inner { get; } = new();
    }

    private static string Body<T>(Expression<T> expression) => expression.Body.Serialize();

    [Fact]
    public void Should_start_each_serialization_from_empty_text()
    {
        var n = Expression.Parameter(typeof(int), "n");
        var serializer = new CSharpExpressionSerializer();

        serializer.Serialize(n);

        serializer.Serialize(n).ShouldBe("n");
    }

    [Fact]
    public void Should_print_a_lambda_with_its_typed_parameters()
    {
        Expression<Func<int, int, bool>> lambda = (a, b) => a > b;

        lambda.Serialize().ShouldBe("(int a, int b) => a > b");
        Body<Func<IEnumerable<int>, IEnumerable<int>>>(xs => xs.Select((x, i) => x + i))
            .ShouldBe("xs.Select((int x, int i) => x + i)");
    }

    [Fact]
    public void Should_print_object_creation()
    {
        Body<Func<int, Box>>(n => new Box(n)).ShouldBe("new Box(n)");
        Body<Func<Box>>(() => new Box()).ShouldBe("new Box()");
    }

    [Fact]
    public void Should_print_object_and_collection_initializers()
    {
        Body<Func<int, Box>>(n => new Box { Value = n, Text = "x" }).ShouldBe("new Box { Value = n, Text = \"x\" }");
        Body<Func<int, Box>>(n => new Box { Items = { n, 2 } }).ShouldBe("new Box { Items = { n, 2 } }");
        Body<Func<int, List<int>>>(n => new List<int> { n, 2 }).ShouldBe("new List<int> { n, 2 }");
    }

    [Fact]
    public void Should_print_array_creation()
    {
        var n = Expression.Parameter(typeof(int), "n");

        Expression.NewArrayBounds(typeof(int), Expression.Constant(3)).Serialize().ShouldBe("new int[3]");
        Expression.NewArrayInit(typeof(int), Expression.Constant(1), n).Serialize().ShouldBe("new int[] { 1, n }");
        Expression.NewArrayInit(typeof(int)).Serialize().ShouldBe("new int[] {}");
    }

    [Fact]
    public void Should_print_a_narrowing_cast_and_omit_a_widening_one()
    {
        Body<Func<long, int>>(l => (int)l).ShouldBe("(int)l");
        Body<Func<long, int>>(l => checked((int)l)).ShouldBe("checked((int)l)");
        Body<Func<int, long>>(n => checked((long)n)).ShouldBe("n");
    }

    [Fact]
    public void Should_print_unary_operators()
    {
        Body<Func<int, int>>(n => -n).ShouldBe("-n");
        Body<Func<int, int>>(n => checked(-n)).ShouldBe("checked(-n)");
        Body<Func<int, int>>(n => ~n).ShouldBe("~n");
        Body<Func<Box, bool>>(b => !b.Flag).ShouldBe("!b.Flag");
    }

    [Fact]
    public void Should_print_unary_operators_only_expression_factories_can_build()
    {
        var n = Expression.Parameter(typeof(int), "n");

        Expression.OnesComplement(n).Serialize().ShouldBe("~n");
        Expression.Increment(n).Serialize().ShouldBe("(n + 1)");
        Expression.PreIncrementAssign(n).Serialize().ShouldBe("(n + 1)");
        Expression.Decrement(n).Serialize().ShouldBe("(n - 1)");
        Expression.PreDecrementAssign(n).Serialize().ShouldBe("(n - 1)");
        Expression.PostIncrementAssign(n).Serialize().ShouldBe("n++");
        Expression.PostDecrementAssign(n).Serialize().ShouldBe("n--");
        Expression.Throw(Expression.New(typeof(Exception))).Serialize().ShouldBe("throw new Exception()");
    }

    [Fact]
    public void Should_print_a_quoted_lambda_as_the_lambda()
    {
        var n = Expression.Parameter(typeof(int), "n");
        var lambda = Expression.Lambda<Func<int, bool>>(Expression.GreaterThan(n, Expression.Constant(1)), n);

        Expression.Quote(lambda).Serialize().ShouldBe("(int n) => n > 1");
    }

    [Fact]
    public void Should_print_type_tests()
    {
        Body<Func<object, bool>>(o => o is string).ShouldBe("o is string");
        Body<Func<object, bool>>(o => !(o is string)).ShouldBe("o is not string");
        Body<Func<object, string?>>(o => o as string).ShouldBe("o as string");
    }

    [Fact]
    public void Should_print_short_circuiting_operators()
    {
        Body<Func<bool, bool, bool>>((a, b) => a && b).ShouldBe("a && b");
        Body<Func<bool, bool, bool>>((a, b) => a || b).ShouldBe("a || b");
    }

    [Fact]
    public void Should_parenthesise_an_operand_only_where_precedence_demands_it()
    {
        Body<Func<int, int>>(n => -(n + 1)).ShouldBe("-(n + 1)");
        Body<Func<int, int>>(n => ~(n + 1)).ShouldBe("~(n + 1)");
        Body<Func<bool, bool, bool>>((a, b) => !(a && b)).ShouldBe("!(a && b)");
        Body<Func<string, int>>(s => (s + s).Length).ShouldBe("(s + s).Length");
        Body<Func<int, int, int, int>>((a, b, c) => (a + b) * c).ShouldBe("(a + b) * c");
        Body<Func<int, int, int, int>>((a, b, c) => a / (b * c)).ShouldBe("a / (b * c)");
        Body<Func<int, int, int, int>>((a, b, c) => a - (b - c)).ShouldBe("a - (b - c)");
        Body<Func<int, int, int>>((a, b) => a - b + 1).ShouldBe("a - b + 1");
        Body<Func<int, int, bool>>((a, b) => (a << 1) > (b >> 2)).ShouldBe("a << 1 > b >> 2");
        Body<Func<int, int, int>>((a, b) => (a | b) & 1).ShouldBe("(a | b) & 1");
        Body<Func<bool, bool, bool, bool>>((a, b, c) => (a || b) && c).ShouldBe("(a || b) && c");
        Body<Func<bool, int, int, int>>((t, a, b) => (t ? a : b) + 1).ShouldBe("(t ? a : b) + 1");
        Body<Func<bool, int, int, int>>((t, a, b) => t ? a : b).ShouldBe("t ? a : b");
    }

    [Fact]
    public void Should_print_invocations_and_array_members()
    {
        var n = Expression.Parameter(typeof(int), "n");

        Expression.Invoke(Expression.Parameter(typeof(Func<int, int>), "f"), n).Serialize().ShouldBe("f(n)");
        Body<Func<int[], int>>(a => a.Length).ShouldBe("a.Length");
    }

    [Fact]
    public void Should_print_an_extension_call_through_a_span_conversion_as_a_call_on_the_array()
    {
        // From C# 14 this binds to MemoryExtensions.Contains over an implicit ReadOnlySpan conversion;
        // earlier compilers bind Enumerable.Contains. Either way the author wrote a.Contains(n).
        Body<Func<int[], int, bool>>((a, n) => a.Contains(n)).ShouldBe("a.Contains(n)");
    }

    [Fact]
    public void Should_print_string_format_as_an_interpolated_string_when_the_format_is_a_constant()
    {
        Body<Func<int, string>>(n => string.Format("a{0}b{1:N2}", n, n)).ShouldBe("$\"a{n}b{n:N2}\"");
        Body<Func<string, int, string>>((format, n) => string.Format(format, n)).ShouldBe("string.Format(format, n)");
    }

    [Fact]
    public void Should_print_an_as_value_parameter_or_constant_by_its_value_without_a_model()
    {
        Body<Func<int, int>>(n => Display.AsValue(n)).ShouldBe("n");
        Body<Func<int, bool>>(n => n > Display.AsValue(5)).ShouldBe("n > 5");
        Body<Func<string?, bool>>(s => s == Display.AsValue((string?)null)).ShouldBe("s == null");
    }

    [Fact]
    public void Should_print_an_unsupported_constant_by_its_to_string()
    {
        Expression.Constant(new Uri("http://example.com/")).Serialize().ShouldBe("http://example.com/");
        Expression.Constant(null, typeof(object)).Serialize().ShouldBe("null");
    }

    [Fact]
    public void Should_print_supported_constants_as_csharp_literals()
    {
        Expression.Constant('c').Serialize().ShouldBe("'c'");
        Expression.Constant(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)).Serialize()
            .ShouldBe("DateTime.Parse(\"2020-01-02T03:04:05.0000000Z\")");
        Expression.Constant(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero)).Serialize()
            .ShouldBe("DateTimeOffset.Parse(\"2020-01-02T03:04:05.0000000+00:00\")");
        Expression.Constant(TimeSpan.FromMinutes(90)).Serialize().ShouldBe("TimeSpan.Parse(\"01:30:00\")");
        Expression.Constant(new Regex("a+")).Serialize().ShouldBe("new Regex(@\"a+\")");
        Expression.Constant(typeof(List<int>)).Serialize().ShouldBe("typeof(List<int>)");
        Expression.Constant(Colour.Green).Serialize().ShouldBe("Colour.Green");
        Expression.Constant(typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)).Serialize().ShouldBe("String.Trim");
    }
}
