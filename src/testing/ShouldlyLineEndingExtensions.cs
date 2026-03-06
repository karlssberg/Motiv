using Shouldly;

/// <summary>
/// Shadows Shouldly's generic <c>ShouldBe&lt;T&gt;</c> for string comparisons so that line-ending
/// differences (LF vs CRLF) never cause a test to fail. Both sides are normalized to LF before
/// comparison, making tests agnostic to source-file encoding and <c>Environment.NewLine</c>.
/// </summary>
internal static class ShouldlyLineEndingExtensions
{
    public static void ShouldBe(this string actual, string expected)
    {
        actual.Replace("\r\n", "\n").ShouldBe(expected.Replace("\r\n", "\n"), (string?)null);
    }

    public static void ShouldBe(this string actual, string expected, string? customMessage)
    {
        ShouldBeStringTestExtensions.ShouldBe(
            actual.Replace("\r\n", "\n"),
            expected.Replace("\r\n", "\n"),
            customMessage);
    }
}
