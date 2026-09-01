namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>A rule document written from a draft, with the paths its nodes were written at.</summary>
/// <param name="Json">The rule document.</param>
/// <param name="NodesByPath">Each draft node keyed by the JSON path it occupies in <paramref name="Json" />.</param>
public sealed record AuthoredDocument(
    string Json,
    IReadOnlyDictionary<string, DraftNode> NodesByPath);
