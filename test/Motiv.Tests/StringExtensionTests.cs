namespace Motiv.Tests;

public class StringExtensionTests
{
    [Theory]
    [InlineData("1, 2, 3, 4, and 5", 1, 2, 3, 4, 5)]
    [InlineData("1, 2, 3, and 4", 1, 2, 3, 4)]
    [InlineData("1, 2, and 3", 1, 2, 3)]
    [InlineData("1 and 2", 1, 2)]
    [InlineData("1", 1)]
    [InlineData("")]
    public void Should_serialize_a_collection_to_a_human_readable_string(string expected, params int[] collection)
    {
        var act = collection.Serialize();

        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1, 2, 3, 4, or 5", true, 1, 2, 3, 4, 5)]
    [InlineData("1, 2, 3, 4 or 5", false, 1, 2, 3, 4, 5)]
    [InlineData("1, 2, 3, or 4", true, 1, 2, 3, 4)]
    [InlineData("1, 2, 3 or 4", false, 1, 2, 3, 4)]
    [InlineData("1, 2, or 3", true, 1, 2, 3)]
    [InlineData("1, 2 or 3", false, 1, 2, 3)]
    [InlineData("1 or 2", true, 1, 2)]
    [InlineData("1 or 2", false, 1, 2)]
    [InlineData("1", true, 1)]
    [InlineData("1", false, 1)]
    [InlineData("", true)]
    [InlineData("", false)]
    public void Should_serialize_a_collection_to_a_human_readable_string_with_parameters(
        string expected,
        bool useOxfordComma,
        params int[] collection)
    {
        var act = collection.Serialize("or", useOxfordComma);

        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", new string?[] { null })]
    [InlineData(" and b", new[] { null, "b" })]
    [InlineData("a and ", new[] { "a", null })]
    [InlineData(" and ", new string?[] { null, null })]
    [InlineData("a, , and c", new[] { "a", null, "c" })]
    [InlineData(", a, and ", new[] { null, "a", null })]
    public void Should_serialize_null_items_as_empty_strings(string expected, string?[] collection)
    {
        var act = collection.Serialize();

        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", new[] { "" })]
    [InlineData(" and b", new[] { "", "b" })]
    [InlineData("a, , and c", new[] { "a", "", "c" })]
    public void Should_serialize_empty_string_items_verbatim(string expected, string[] collection)
    {
        var act = collection.Serialize();

        act.ShouldBe(expected);
    }
}
