using System.Drawing;
using System.Linq.Expressions;
using System.Reflection;

namespace Motiv.Tests;

public class ExpressionTreeTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void Should_recompose_a_lambda_expression_tree_into_a_proposition(int model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((int n) => n > 0)
                .Create("is-positive");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Should_recompose_a_lambda_expression_tree_into_a_tree_of_multiple_propositions(int model,
        bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((int n) => n > 0 && n % 2 == 0)
                .Create("is-positive-and-even");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(-1, "n < 0")]
    [InlineData(0, "n == 0")]
    [InlineData(1, "n % 2 != 0")]
    [InlineData(2, "n > 0", "n % 2 == 0")]
    [InlineData(3, "n % 2 != 0")]
    [InlineData(4, "n > 0", "n % 2 == 0")]
    public void Should_reveal_the_underlying_assertions(int model, params string[] expectedAssertion)
    {
        // Arrange
        var sut =
            Spec.From((int n) => (n > 0) & (n <= 5) && n % 2 == 0)
                .Create("is-positive-and-even");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().Contain(expectedAssertion);
    }

    [Fact]
    public void Should_perform_text_search_using_literals()
    {
        const string quickVariable = "quick";
        var sut =
            Spec.From((string txt) => txt.Contains(quickVariable) | txt.Contains("brown") | txt.Contains("pink"))
                .Create("text-search");

        // Act
        var act = sut.IsSatisfiedBy("the quick brown fox jumps over the lazy dog");

        // Assert
        act.Assertions.Should().BeEquivalentTo(
            [
                """txt.Contains("quick") == true""",
                """txt.Contains("brown") == true""",
            ]);
    }

    [Theory]
    [InlineData("quick", """txt.Contains("quick") == true""")]
    [InlineData("brown", """txt.Contains("brown") == true""")]
    [InlineData("pink", """txt.Contains("pink") == false""")]
    public void Should_perform_text_search(string searchTerm, string expectedAssertion)
    {
        var sut =
            Spec.From((string txt) => txt.Contains(searchTerm))
                .Create("text-search");

        // Act
        var act = sut.IsSatisfiedBy("the quick brown fox jumps over the lazy dog");

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("hello", 2, "txt.Count<char>(ch => vowels.Contains(ch)) == 2")]
    [InlineData("world", 2, "txt.Count<char>(ch => vowels.Contains(ch)) < 2")]
    [InlineData("high-roller", 2, "txt.Count<char>(ch => vowels.Contains(ch)) > 2")]
    public void Should_display_nested_function_expression(string model, uint threshold, string expectedAssertion)
    {
        // Assemble
        var vowels = new HashSet<char>("aeiouAEIOU");

        var sut = Spec
            .From((string txt) => txt.Count(ch => vowels.Contains(ch)) > threshold)
            .Create($"has-more-than-{threshold}-vowels");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 != 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 != 2")]
    public void Should_assert_expressions_containing_an_equals_comparison(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 == 2)
            .Create("equals-test");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 != 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 != 2")]
    public void Should_assert_expressions_containing_a_not_equals_comparison(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 != 2)
            .Create("not-equals-test");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 < 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 > 2")]
    public void Should_assert_expressions_containing_a_greater_than_comparison(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 > 2)
            .Create("greater-than-test");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 < 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 > 2")]
    public void Should_assert_expressions_containing_a_greater_than_or_equals_comparison(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 >= 2)
            .Create("greater-than-or-equals-test");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 < 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 > 2")]
    public void Should_assert_expressions_containing_a_less_than_comparison(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 < 2)
            .Create("less-than-test");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 < 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 > 2")]
    public void Should_assert_expressions_containing_a_less_than_or_equals_comparison(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 <= 2)
            .Create("less-than-or-equals-test");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "n + 1 < 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 > 2")]
    public void Should_assert_expressions_containing_a_constant_expression(int model, string expectedAssertion)
    {
        // Assemble
        var threshold = 2;
        var sut = Spec
            .From((int n) => n + 1 <= threshold)
            .Create("assert-constant");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(0, "list.Count<int>(i => i > n) > 2")]
    [InlineData(1, "list.Count<int>(i => i > n) == 2")]
    [InlineData(2, "list.Count<int>(i => i > n) < 2")]
    public void Should_assert_expressions_containing_a_parameter_expression(int model, string expectedAssertion)
    {
        // Assemble
        var list = new List<int> { 1, 2, 3 };
        var threshold = 2;
        var sut = Spec
            .From((int n) => list.Count(i => i > n) <= threshold)
            .Create("assert-parameter");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }


    [Theory]
    [InlineData(0, "n + 1 < 2")]
    [InlineData(1, "n + 1 == 2")]
    [InlineData(2, "n + 1 > 2")]
    public void Should_assert_expressions_containing_a_binary_expression(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n + 1 <= 2)
            .Create("assert-binary");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(2, "-n < -1")]
    [InlineData(1, "-n == -1")]
    [InlineData(0, "-n > -1")]
    public void Should_assert_expressions_containing_a_unary_expression(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => -n >= -1)
            .Create("assert-unary");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("ab", "txt.Substring(1).Length < 2")]
    [InlineData("abc", "txt.Substring(1).Length == 2")]
    [InlineData("abcd", "txt.Substring(1).Length > 2")]
    public void Should_assert_expressions_containing_a_method_call_expression(string model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((string txt) => txt.Substring(1).Length > 2)
            .Create("assert-method-call");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("", "txt.Length == 0", "txt == \"\"")]
    [InlineData("a", "txt.Length != 0", "tryParseInt(txt) == false")]
    [InlineData("123", "txt.Length != 0", "tryParseInt(txt) == true")]
    public void Should_assert_expressions_containing_a_conditional_expression(string model, params string[] expectedAssertion)
    {
        // Assemble
        var tryParseInt = (string s) => int.TryParse(s, out _);
        var sut = Spec
            .From((string txt) => txt.Length == 0 ? txt == "" : tryParseInt(txt))
            .Create("assert-conditional");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(1,  "obj.GetType().IsEnum == false")]
    [InlineData(BindingFlags.Public,  "obj.GetType().IsEnum == true")]
    public void Should_assert_expressions_containing_a_member_expression(object model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((object obj) => obj.GetType().IsEnum)
            .Create("assert-member");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("", "new List<int> { 1, 2, 3 }.Count != txt.Length")]
    [InlineData("aaa", "new List<int> { 1, 2, 3 }.Count == txt.Length")]
    public void Should_assert_expressions_containing_a_new_expressions_with_list_initializer(string model, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((string txt) => new List<int> { 1, 2, 3 }.Count == txt.Length)
            .Create("assert-new-with-list-initializer");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("Moe", """new string[] { "Moe", "Larry", "Curly" }.Contains<string>(name) == true""")]
    [InlineData("Joe", """new string[] { "Moe", "Larry", "Curly" }.Contains<string>(name) == false""")]
    public void Should_assert_expressions_containing_a_new_array_expression(string model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((string name) => new [] { "Moe", "Larry", "Curly" }.Contains(name))
            .Create("new-array-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(3, "n != new int[] {}.Length")]
    [InlineData(0, "n == new int[] {}.Length")]
    public void Should_assert_expressions_containing_a_new_array_expression_with_an_empty_array(int model, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => n == new int[] {}.Length)
            .Create("new-array-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(6, "new int[] { 1, 2, 3, 4, 5, 6 }.Length == n")]
    [InlineData(0, "new int[] { 1, 2, 3, 4, 5, 6 }.Length != n")]
    public void Should_assert_expressions_containing_a_new_array_expression_with_max_size(int model, string expectedAssertion)
    {
        var x = Spec.Build((int n ) => n > 0).Create("n positive");
        // Assemble
        var sut = Spec
            .From((int n) => new[] { 1, 2, 3, 4, 5, 6 }.Length == n)
            .Create("new-array-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(3, "(num => num < 3)(n) == false")]
    [InlineData(2, "(num => num < 3)(n) == true")]
    public void Should_assert_expressions_containing_a_lambda_expression_with_invocation(int model, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => ((Func<int, bool>)(num => num < 3))(n))
            .Create("lambda-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("3", "obj is string == true")]
    [InlineData(3, "obj is string == false")]
    public void Should_assert_expressions_containing_a_type_comparison(object model, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((object obj) => obj is string)
            .Create("type-binary-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(2, 3, "new List<int> { 1, 2, 3 }[index] == 3")]
    [InlineData(1, 1, "new List<int> { 1, 2, 3 }[index] != 1")]
    public void Should_assert_expressions_containing_an_index_expression(int model, int query, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int index) => new List<int> { 1, 2, 3 }[index] == query)
            .Create("index-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(3, 3, "(default(int?) ?? 3) == n")]
    [InlineData(0, 1, "(default(int?) ?? 1) != n")]
    public void Should_assert_expressions_containing_a_coalesce_expression(int model, int substitution, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => (default(int?) ?? substitution) == n)
            .Create("coalesce_expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    Expression<Func<int, string, bool, List<int>, double>> complexExpression =
        (x, s, b, list) =>


            // Coalesce Expression
            (default(int?) ?? 0) +

            // Member Init Expression
            new Point { X = x, Y = x * 2 }.X +

            // Aggregate function (using LINQ)
            list.Sum() +

            // Cast Expression
            ((double)x / 100);

    private Expression<Func<int, bool>> complexPredicate = (int n) =>
        // Equality and logical operators
        n == 0 && n != 1 || n == 2 && !(n < 0) &&

        // Arithmetic operations with type conversion
        (double)n / 2 == Math.Floor((double)n / 2) &&

        // Bitwise operations
        ((n & 1) == 0) && ((n | 4) > 0) && ((n ^ 3) != 0) &&

        // Shift operations
        ((n << 2) > (n >> 1)) &&

        // Conditional operator
        (n % 2 == 0 ? n / 2 : n * 3 + 1) < 100 &&

        // Method calls (static and instance)
        Math.Abs(n) == n && n.ToString().Length <= 3 &&

        // Property access
        n.GetType().Name == "Int32" &&

        // Array creation and indexing
        new[] { 1, 2, 3, 4, 5 }[Math.Abs(n) % 5] == n &&

        // String interpolation and comparison
        $"{n:D2}" == (n < 10 ? "0" + n : n.ToString()) &&

        // Nullable types
        (int?)n == (int?)0 &&

        // Enum comparison (assuming System.DayOfWeek is available)
        (DayOfWeek)((n % 7) + 1) != DayOfWeek.Sunday &&

        // Constant folding
        n * 1 == 1 * n && n + 0 == 0 + n &&

        // Complex nested expressions
        ((n > 0 ? n * n : -n * n) + (n < 0 ? -1 : 1)) % (Math.Abs(n) + 1) == 0 &&

        // Use of constants
        n * Math.PI < 10 && n * Math.E < 10 &&

        // Bit counting (population count)
        Convert.ToString(n, 2).Count(c => c == '1') < 5 &&

        // Use of other numeric types
        (long)n * (long)n <= int.MaxValue &&

        // String operations
        ("0123456789".IndexOf(n.ToString()) >= 0) &&

        // Use of char
        (char)(n + 'A') <= 'Z';
}
