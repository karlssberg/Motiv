using System.Xml.Linq;

namespace Motiv.Tests;

/// <summary>
/// Guards the XML documentation file that <c>Motiv</c> ships to consumers.
///
/// <para>
/// What these cases can see is the <em>artifact</em>: <c>GenerateDocumentationFile</c> makes the
/// compiler emit <c>Motiv.xml</c>, the SDK copies it next to <c>Motiv.dll</c> for every referencing
/// project, and <c>pack</c> places it beside the assembly in the package — which is where a
/// consumer's IDE reads IntelliSense from. Remove the property and these go red.
/// </para>
///
/// <para>
/// What they cannot see is the reason the property was turned on: the same flag enables the
/// compiler's doc-comment binding pass, which resolves every <c>&lt;see cref&gt;</c> and checks
/// every <c>&lt;param&gt;</c> against the real signature. That half is enforced by <c>csc</c> and
/// <c>TreatWarningsAsErrors</c>, so it fails the build rather than a test, and no assertion here
/// distinguishes a tree with sound crefs from one whose crefs are never bound. A green run here
/// means the file ships, not that its contents resolve.
/// </para>
/// </summary>
public class XmlDocumentationTests
{
    private static readonly string DocumentationPath =
        Path.Combine(AppContext.BaseDirectory, "Motiv.xml");

    [Fact]
    public void Should_emit_an_xml_documentation_file_alongside_the_assembly()
    {
        File.Exists(DocumentationPath).ShouldBeTrue(
            $"Motiv.xml was not copied to '{AppContext.BaseDirectory}'. This is what a consumer's " +
            "IDE reads IntelliSense from, and it exists only while Motiv.csproj sets " +
            "GenerateDocumentationFile — the same property that makes the compiler validate crefs.");
    }

    [Fact]
    public void Should_document_the_public_surface_rather_than_emit_an_empty_stub()
    {
        var document = XDocument.Load(DocumentationPath);

        var documentedMembers = document
            .Descendants("member")
            .Select(member => member.Attribute("name")?.Value)
            .ToList();

        documentedMembers.ShouldContain("T:Motiv.Spec`2");
    }

    [Fact]
    public void Should_name_the_assembly_it_documents()
    {
        var document = XDocument.Load(DocumentationPath);

        // Bound and defaulted before asserting, deliberately. Written as one `?.` chain ending in
        // ShouldBe, the whole expression short-circuits to null when any link is missing and the
        // assertion is never invoked — a case that cannot fail on content, only on Load throwing.
        // The `??` keeps a missing element failing on the assertion, and says which one.
        var assemblyName = document.Root?.Element("assembly")?.Element("name")?.Value
                           ?? "<no assembly/name element>";

        assemblyName.ShouldBe("Motiv");
    }
}
