namespace Motiv.Serialization;

/// <summary>
/// Describes a spec registered with a <see cref="SpecRegistry" />: its stable name, model and
/// metadata types, and whether it evaluates asynchronously.
/// </summary>
public sealed class SpecRegistryEntry
{
    internal SpecRegistryEntry(string name, Type modelType, Type metadataType, bool isAsync, object spec, string? description = null)
    {
        Name = name;
        ModelType = modelType;
        MetadataType = metadataType;
        IsAsync = isAsync;
        Spec = spec;
        Description = description;
    }

    /// <summary>The stable name that rule documents use to reference the spec.</summary>
    public string Name { get; }

    /// <summary>The model type the spec evaluates against.</summary>
    public Type ModelType { get; }

    /// <summary>The metadata type the spec yields.</summary>
    public Type MetadataType { get; }

    /// <summary>Whether the spec evaluates asynchronously.</summary>
    public bool IsAsync { get; }

    /// <summary>An optional human-readable description surfaced in a catalog UI.</summary>
    public string? Description { get; }

    internal object Spec { get; }
}
