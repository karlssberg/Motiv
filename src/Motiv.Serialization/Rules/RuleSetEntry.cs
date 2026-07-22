namespace Motiv.Serialization;

/// <summary>A read-only listing of one rule in a <see cref="RuleSet"/>.</summary>
/// <param name="Name">The stable rule name.</param>
/// <param name="ModelType">The model type the rule evaluates against.</param>
/// <param name="MetadataType">The metadata type the rule yields.</param>
/// <param name="IsAsync">Whether the rule evaluates asynchronously.</param>
/// <param name="IsPolicy">Whether the rule yields a single value.</param>
/// <param name="Version">The current version.</param>
/// <param name="Description">The optional description.</param>
/// <param name="DocumentJson">The current document, or null while on a compiled default.</param>
public sealed record RuleSetEntry(
    string Name,
    Type ModelType,
    Type MetadataType,
    bool IsAsync,
    bool IsPolicy,
    int Version,
    string? Description,
    string? DocumentJson)
{
    /// <summary>The stable rule name.</summary>
    public string Name { get; } = Name;

    /// <summary>The model type the rule evaluates against.</summary>
    public Type ModelType { get; } = ModelType;

    /// <summary>The metadata type the rule yields.</summary>
    public Type MetadataType { get; } = MetadataType;

    /// <summary>Whether the rule evaluates asynchronously.</summary>
    public bool IsAsync { get; } = IsAsync;

    /// <summary>Whether the rule yields a single value.</summary>
    public bool IsPolicy { get; } = IsPolicy;

    /// <summary>The current version.</summary>
    public int Version { get; } = Version;

    /// <summary>The optional description.</summary>
    public string? Description { get; } = Description;

    /// <summary>The current document, or null while on a compiled default.</summary>
    public string? DocumentJson { get; } = DocumentJson;
}
