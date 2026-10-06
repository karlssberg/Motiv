using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Printing;

/// <summary>
/// A payload text with a brace that is neither a hole nor an escaped pair. The binder refuses such a
/// document, but the print must still compile, so the brace is printed as a literal and a warning says why.
/// </summary>
public class CSharpPrinterBraceTests
{
    public sealed record Customer(bool IsActive);

    private static CSharpPrintedRule Print(string whenTrue, string whenFalse = "no") =>
        CSharpPrinter.Print(
            $$"""{ "rule": { "spec": "a", "whenTrue": {{System.Text.Json.JsonSerializer.Serialize(whenTrue)}}, "whenFalse": {{System.Text.Json.JsonSerializer.Serialize(whenFalse)}} } }""",
            new CSharpPrintOptions { ModelType = typeof(Customer), SpecHandles = new Dictionary<string, string> { ["a"] = "A" } });

    [Theory]
    [InlineData("a } b", "$\"a }} b\"")]
    [InlineData("a { b", "$\"a {{ b\"")]
    [InlineData("} then {x}", "$\"}} then {x}\"")]
    public void Should_escape_an_unmatched_brace_so_the_print_compiles(string text, string expected)
    {
        Print(text).Source.ShouldContain($"WhenTrue({expected})");
    }

    [Fact]
    public void Should_warn_that_a_document_with_an_unmatched_brace_does_not_bind()
    {
        Print("a } b").Warnings.ShouldBe(
            ["$.rule.whenTrue: unmatched '}' printed as a literal brace; the document does not bind until it is escaped as '}}'"]);
        Print("a { b").Warnings.ShouldBe(
            ["$.rule.whenTrue: unmatched '{' printed as a literal brace; the document does not bind until it is escaped as '{{'"]);
    }

    [Fact]
    public void Should_name_the_whenFalse_path_when_the_unmatched_brace_is_in_whenFalse()
    {
        Print("yes", whenFalse: "a } b").Warnings.ShouldBe(
            ["$.rule.whenFalse: unmatched '}' printed as a literal brace; the document does not bind until it is escaped as '}}'"]);
    }
}
