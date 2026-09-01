using Motiv.RuleAuthoring.Blazor.Domain;
using Motiv.Serialization;

namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>
/// Authors a rule document from a draft and takes it through Motiv.Serialization — validate, then
/// bind and evaluate — with nothing else in the loop.
/// </summary>
public sealed class AuthoringSession
{
    private readonly RuleSerializer _serializer = new(CustomerVocabulary.Registry());

    /// <summary>Writes, validates and — if it is valid — evaluates the draft.</summary>
    /// <param name="root">The root of the draft tree.</param>
    /// <param name="name">The document name.</param>
    /// <param name="customer">The model to evaluate a valid document against.</param>
    /// <returns>The document, its located errors, and the evaluation if there were none.</returns>
    public AuthoringOutcome Author(DraftNode root, string name, Customer customer)
    {
        var document = RuleDocumentWriter.Write(root, name);
        var errors = _serializer.Validate<Customer>(document.Json);

        if (errors.Count > 0)
            return AuthoringOutcome.Invalid(document.Json, Locate(errors, document));

        var result = _serializer.Deserialize<Customer>(document.Json).Evaluate(customer);

        return AuthoringOutcome.Evaluated(
            document.Json,
            result.Satisfied,
            result.Reason,
            result.Justification);
    }

    private static IReadOnlyList<LocatedError> Locate(
        IReadOnlyList<RuleError> errors,
        AuthoredDocument document) =>
        [.. errors.Select(error => new LocatedError(error, ResolveNode(error.Path, document)))];

    /// <summary>
    /// Resolves the node an error's path names, falling back to the nearest enclosing node.
    /// </summary>
    /// <remarks>
    /// A path can name a node's property — <c>$.rule.and[1].whenTrue</c> — rather than the node
    /// itself, and an authoring UI still has to put that error beside <c>and[1]</c>.
    /// </remarks>
    private static DraftNode? ResolveNode(string path, AuthoredDocument document)
    {
        for (var candidate = path; candidate.Length > 0; candidate = TrimLastSegment(candidate))
            if (document.NodesByPath.TryGetValue(candidate, out var node))
                return node;

        return null;
    }

    private static string TrimLastSegment(string path)
    {
        var cut = path.LastIndexOfAny(['.', '[']);
        return cut <= 0 ? "" : path.Substring(0, cut);
    }
}
