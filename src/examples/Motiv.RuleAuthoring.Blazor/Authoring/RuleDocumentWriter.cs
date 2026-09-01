using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>Writes a <see cref="DraftNode" /> tree as a Motiv rule document.</summary>
public static class RuleDocumentWriter
{
    /// <summary>Writes the draft as a rule document.</summary>
    /// <param name="root">The root of the draft tree.</param>
    /// <param name="name">The document name.</param>
    /// <returns>The JSON and the path each draft node was written at.</returns>
    /// <remarks>
    /// The paths are recorded by the same walk that emits the JSON. A separately derived path could
    /// disagree with the one a <c>RuleError</c> names, and would do so silently.
    /// </remarks>
    public static AuthoredDocument Write(DraftNode root, string name)
    {
        var nodesByPath = new Dictionary<string, DraftNode>(StringComparer.Ordinal);
        var buffer = new ArrayBufferWriter<byte>();

        // Indented: the sample shows the document to its author, and a document nobody can read
        // teaches nothing. Validation is indifferent to the whitespace.
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WritePropertyName("rule");
            WriteNode(writer, root, "$.rule", nodesByPath);
            writer.WriteEndObject();
        }

        return new AuthoredDocument(Encoding.UTF8.GetString(buffer.WrittenSpan), nodesByPath);
    }

    private static void WriteNode(
        Utf8JsonWriter writer,
        DraftNode node,
        string path,
        Dictionary<string, DraftNode> nodesByPath)
    {
        nodesByPath[path] = node;

        writer.WriteStartObject();

        if (node.Kind is DraftNodeKind.Spec)
        {
            writer.WriteString("spec", node.SpecName);
        }
        else
        {
            var keyword = DraftNodeKinds.Keyword(node.Kind);
            writer.WritePropertyName(keyword);
            WriteOperands(writer, node, $"{path}.{keyword}", nodesByPath);
        }

        writer.WriteEndObject();
    }

    private static void WriteOperands(
        Utf8JsonWriter writer,
        DraftNode node,
        string keywordPath,
        Dictionary<string, DraftNode> nodesByPath)
    {
        if (DraftNodeKinds.IsUnary(node.Kind))
        {
            WriteNode(writer, node.Children[0], keywordPath, nodesByPath);
            return;
        }

        writer.WriteStartArray();
        for (var i = 0; i < node.Children.Count; i++)
            WriteNode(writer, node.Children[i], $"{keywordPath}[{i}]", nodesByPath);
        writer.WriteEndArray();
    }
}
