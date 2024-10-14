using System.Drawing;
using System.Globalization;
using System.Reflection;
using Motiv.ExpressionTrees;

namespace Motiv.Tests;

public class ExpressionTreeAssertionTests
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
    [InlineData(-1, "n <= 0")]
    [InlineData(0, "n <= 0")]
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
            Spec.From((string txt) => txt.Contains(Display.AsValue(searchTerm)))
                .Create("text-search");

        // Act
        var act = sut.IsSatisfiedBy("the quick brown fox jumps over the lazy dog");

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("quick", """txt.Contains(searchTerm) == true""")]
    [InlineData("brown", """txt.Contains(searchTerm) == true""")]
    [InlineData("pink", """txt.Contains(searchTerm) == false""")]
    public void Should_serialize_a_variable_name(string searchTerm, string expectedAssertion)
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
    [InlineData("world", 2, "txt.Count((char ch) => vowels.Contains(ch)) <= threshold")]
    [InlineData("high-roller", 2, "txt.Count((char ch) => vowels.Contains(ch)) > threshold")]
    public void Should_display_nested_function_expression(string model, int threshold, string expectedAssertion)
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
    [InlineData(0, "n + 1 <= 2")]
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
    [InlineData(2, "n + 1 >= 2")]
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
    [InlineData(2, "n + 1 >= 2")]
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
    [InlineData(0, "n + 1 <= 2")]
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
    [InlineData(0, "n + 1 <= threshold")]
    [InlineData(2, "n + 1 > threshold")]
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
    [InlineData(0, "list.Count((int i) => i > n) > threshold")]
    [InlineData(2, "list.Count((int i) => i > n) <= threshold")]
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
    [InlineData(0, "n + 1 <= 2")]
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
    [InlineData(0, "-n >= -1")]
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
    [InlineData("ab", "txt.Substring(1).Length <= 2")]
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
    [InlineData("Moe", """new string[] { "Moe", "Larry", "Curly" }.Contains(name) == true""")]
    [InlineData("Joe", """new string[] { "Moe", "Larry", "Curly" }.Contains(name) == false""")]
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
    [InlineData(3, "((int num) => num < 3)(n) == false")]
    [InlineData(2, "((int num) => num < 3)(n) == true")]
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
    [InlineData(2, 3, "new List<int> { 1, 2, 3 }[index] == query")]
    [InlineData(1, 1, "new List<int> { 1, 2, 3 }[index] != query")]
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
    [InlineData(3, 3, "(null ?? substitution) == n")]
    [InlineData(0, 1, "(null ?? substitution) != n")]
    [InlineData(0, 0, "(null ?? substitution) == n")]
    [InlineData(1, 0, "(null ?? substitution) != n")]
    public void Should_assert_expressions_containing_a_coalesce_expression(int model, int substitution, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((int n) => (default(int?) ?? substitution) == n)
            .Create("coalesce-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(2, 3, 2, "new Point { X = x, Y = y * 2 }.X != query")]
    [InlineData(1, 1, 3, "new Point { X = x, Y = y * 2 }.X == query")]
    public void Should_assert_expressions_containing_a_member_init_expression(int model, int x, int y, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((int query) => new Point { X = x, Y = y * 2 }.X == query)
            .Create("member-init-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(2, 3, "x / 100 < query")]
    [InlineData(1, 1, "x / 100 < query")]
    public void Should_assert_expressions_containing_a_cast(int model, int x, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((int query) => (double)x / 100 < query)
            .Create("casting-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(100, $$"""
                      $"{n:D2}" == (n < 10 ? $"0{n}" : $"{n}")
                      """)]
    [InlineData(10, $$"""
                     $"{n:D2}" == (n < 10 ? $"0{n}" : $"{n}")
                     """)]
    [InlineData(1, """
                   $"{n:D2}" == (n < 10 ? $"0{n}" : $"{n}")
                   """)]
    public void Should_assert_expressions_containing_string_interpolation(int model, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((int n) => $"{n:D2}" == (n < 10 ? $"0{n}" : $"{n}"))
            .Create("string-interpolation-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(1, 1, "((p.Item1 - 1) * (p.Item2 - -1)) / 2 < p.Item1 * p.Item2")]
    [InlineData(2, 0, "((p.Item1 - 1) * (p.Item2 - -1)) / 2 >= p.Item1 * p.Item2")]
    [InlineData(3, 3, "((p.Item1 - 1) * (p.Item2 - -1)) / 2 < p.Item1 * p.Item2")]
    public void Should_assert_expressions_containing_arithmetic(decimal x, decimal y, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From(((decimal x, decimal y) p) => (p.x - 1) * (p.y - -1) / 2 < p.x * p.y)
            .Create("arithmetic-expression");

        // Act
        var act = sut.IsSatisfiedBy((x, y));

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(1, "(n & 1) != 0")]
    [InlineData(0, "(n & 1) == 0", "(n | 4) > 0", "(n ^ 3) != 0")]
    public void Should_assert_expressions_containing_bitwise_operations(long model, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((long n) => (n & 1) == 0 & (n | 4) > 0 & (n ^ 3) != 0)
            .Create("bitwise-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(-1, "n << 2 <= n >> 1")]
    [InlineData(1, "n << 2 > n >> 1")]
    public void Should_assert_expressions_containing_bit_shift_operations(int model, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((int n) => n << 2 > n >> 1)
            .Create("bit-shift-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(BindingFlags.Public, "f.HasFlag(BindingFlags.Public) == true")]
    [InlineData(BindingFlags.NonPublic | BindingFlags.Instance, "f.HasFlag(BindingFlags.Public) == false", "f.HasFlag(BindingFlags.Static) == false")]
    [InlineData(BindingFlags.Public | BindingFlags.Static, "f.HasFlag(BindingFlags.Public) == true", "f.HasFlag(BindingFlags.Static) == true")]
    public void Should_assert_expressions_containing_enums(BindingFlags flags, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((BindingFlags f) => f.HasFlag(BindingFlags.Public) | f.HasFlag(BindingFlags.Static))
            .Create("enum-expression");

        // Act
        var act = sut.IsSatisfiedBy(flags);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(null, "n == null")]
    [InlineData(0, "n == 0")]
    [InlineData(int.MaxValue, "n != null", "n != 0")]
    public void Should_assert_expressions_containing_nullable_types(int? model, params string[] expectedAssertion)
    {
        // Assembled
        var sut = Spec
            .From((int? n) => n == null | n == 0)
            .Create("nullable-expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineAutoData("2023-07-15T08:30:45+03:00", "date < now", "now < DateTime.Parse(\"3023-07-15T05:30:45.0000000Z\")")]
    [InlineAutoData("3023-07-15T05:30:45Z", "date >= now")]
    public void Should_assert_expressions_containing_datetime_values(DateTime date, params string[] expectedAssertion)
    {
        // Assembled
        var futureDate = DateTime.Parse(
            "3023-07-15T05:30:45Z",
            DateTimeFormatInfo.InvariantInfo,
            DateTimeStyles.AdjustToUniversal);

        var sut = Spec
            .From((DateTime now) => date < now && now < Display.AsValue(futureDate))
            .Create("datetime-expression");

        // Act
        var act = sut.IsSatisfiedBy(DateTime.UtcNow);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("world", 2, "\"world\".Count((char ch) => vowels.Contains(ch)) <= 2")]
    [InlineData("high-roller", 2, "\"high-roller\".Count((char ch) => vowels.Contains(ch)) > 2")]
    public void Should_serialize_parameter_as_value_when_requested(string model, int threshold, string expectedAssertion)
    {
        // Assemble
        var vowels = new HashSet<char>("aeiouAEIOU");

        var sut = Spec
            .From((string txt) => Display.AsValue(txt).Count(ch => vowels.Contains(ch)) > Display.AsValue(threshold))
            .Create($"has-more-than-{threshold}-vowels");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("hello", 2, "(string.IsNullOrEmpty(txt) ? \"\" : \"hello\").Length > threshold")]
    [InlineData("high-roller", 2, "(string.IsNullOrEmpty(txt) ? \"\" : \"high-roller\").Length > threshold")]
    public void Should_serialize_values_in_a_conditional_expression(string model, int threshold, string expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((string txt) => (string.IsNullOrEmpty(txt) ? "" : Display.AsValue(txt)).Length > threshold)
            .Create($"length-greater-than-{threshold}");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("hello world", "hello", "contains 'hello'")]
    [InlineData("high-roller", "hello", "does not contain 'hello'")]
    public void Should_treat_boolean_results_within_expressions_as_a_source_of_custom_assertions(string text, string model, params string[] expectedAssertion)
    {
        // Assemble
        var textEquals = Spec
            .Build((string query) => text.Contains(query))
            .WhenTrue(query => $"contains '{query}'")
            .WhenFalse(query => $"does not contain '{query}'")
            .Create($"text-equals")
            .IsSatisfiedBy(model);

        var tuple =(Spec: textEquals, query: model);

        var sut = Spec
            .From((string query) => tuple.Spec)
            .Create("supports-embedded-specifications");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("hello world", "hello", "query.Length > 0", "contains 'hello'")]
    [InlineData("high-roller", "world", "does not contain 'world'")]
    public void Should_treat_specifications_within_expressions_as_a_source_of_custom_assertions(string text, string model, params string[] expectedAssertion)
    {
        // Assemble
        var textEquals = Spec
            .Build((string query) => text.Contains(query))
            .WhenTrue(query => $"contains '{query}'")
            .WhenFalse(query => $"does not contain '{query}'")
            .Create($"text-equals");

        var sut = Spec
            .From((string query) => query.Length > 0 && textEquals.IsSatisfiedBy(query))
            .Create("supports-embedded-specifications");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("hello world", "hello", "query.Length > 3", "contains 'hello'")]
    [InlineData("high-roller", "world", "does not contain 'world'")]
    public void Should_treat_boolean_result_factory_within_expressions_as_a_source_of_custom_assertions(string text, string model, params string[] expectedAssertion)
    {
        // Assemble
        var textEquals = (string m) => Spec
            .Build((string query) => text.Contains(query))
            .WhenTrue(query => $"contains '{query}'")
            .WhenFalse(query => $"does not contain '{query}'")
            .Create($"text-equals")
            .IsSatisfiedBy(m);

        var sut = Spec
            .From((string query) => query.Length > 3 && textEquals(query))
            .Create("supports-embedded-specifications");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("hello world", "hello", "contains 'hello'")]
    [InlineData("high-roller", "world", "does not contain 'world'")]
    public void Should_treat_boolean_result_model_as_a_source_of_custom_assertions(string text, string model, params string[] expectedAssertion)
    {
        // Assemble
        var textEquals = Spec
            .Build((string query) => text.Contains(query))
            .WhenTrue(query => $"contains '{query}'")
            .WhenFalse(query => $"does not contain '{query}'")
            .Create($"text-equals")
            .IsSatisfiedBy(model);

        var sut = Spec
            .From((BooleanResultBase<string> equal) => equal)
            .Create("supports-embedded-specifications");

        // Act
        var act = sut.IsSatisfiedBy(textEquals);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }


    [Theory]
    [InlineData("hello world", "hello", "contains 'hello'")]
    [InlineData("high-roller", "world", "does not contain 'world'")]
    public void Should_override_assertions_with_custom_assertions_when_using_higher_order_propositions(string text, string model, params string[] expectedAssertion)
    {
        // Assemble
        var sut = Spec
            .From((string query) => text.Contains(query))
            .AsAnySatisfied()
            .WhenTrueYield(eval => eval.CausalModels
                .Select(query => $"contains '{query}'"))
            .WhenFalseYield(eval => eval.CausalModels
                .Select(query => $"does not contain '{query}'"))
            .Create($"text-equals");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("is admin", "admin", "user")]
    [InlineData("is not admin", "user")]
    public void Should_convert_any_linq_function_to_higher_order_proposition(string expectedAssertion, params string[] model)
    {
        // Assemble
        var isAdmin =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin");

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.Any(isAdmin))
                .Create("has admin");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("is admin", "admin", "user")]
    [InlineData("is not admin", "user")]
    public void Should_convert_any_linq_function_to_higher_order_proposition_using_its_IsSatisfiedBy_method(string expectedAssertion, params string[] model)
    {
        // Assemble
        var isAdmin =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin");

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.Any(role => isAdmin.IsSatisfiedBy(role)))
                .Create("has admin");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("is admin", "admin", "admin")]
    [InlineData("is not admin", "user, admin")]
    public void Should_convert_all_linq_function_to_higher_order_proposition(string expectedAssertion, params string[] model)
    {
        // Assemble
        var isAdmin =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin");

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.All(isAdmin))
                .Create("all admins");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("is admin", "admin", "admin")]
    [InlineData("is not admin", "user, admin")]
    public void Should_convert_all_linq_function_to_higher_order_proposition_using_its_IsSatisfiedBy_method(string expectedAssertion, params string[] model)
    {
        // Assemble
        var isAdmin =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin");

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.All(role => isAdmin.IsSatisfiedBy(role)))
                .Create("all admins");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }
}
