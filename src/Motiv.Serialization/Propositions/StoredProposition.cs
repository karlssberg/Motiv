namespace Motiv.Serialization;

/// <summary>
/// One authored proposition as it is persisted. The model type is carried explicitly because it is
/// not part of the document — a rule takes its model from the C# class that declares it, and an
/// authored proposition has no such class.
/// </summary>
/// <param name="Name">The dot-separated name documents reference the proposition by.</param>
/// <param name="ModelType">The registered model-type id the document binds against.</param>
/// <param name="DocumentJson">The rule document defining the proposition.</param>
/// <param name="Version">The document's revision, starting at 1.</param>
/// <param name="Description">An optional human-readable description surfaced in the catalog.</param>
public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);
