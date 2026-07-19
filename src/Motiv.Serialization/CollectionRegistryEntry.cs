namespace Motiv.Serialization;

/// <summary>Describes a collection projection registered on a <see cref="SpecRegistry"/>.</summary>
public sealed class CollectionRegistryEntry
{
    internal CollectionRegistryEntry(Type parentType, string path, Type elementType)
    {
        ParentType = parentType;
        Path = path;
        ElementType = elementType;
    }

    /// <summary>The model type the collection is selected from.</summary>
    public Type ParentType { get; }

    /// <summary>The path higher-order rule nodes reference the collection by.</summary>
    public string Path { get; }

    /// <summary>The element type of the collection.</summary>
    public Type ElementType { get; }
}
