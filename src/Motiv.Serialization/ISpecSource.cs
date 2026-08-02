namespace Motiv.Serialization;

/// <summary>
/// Where a binder resolves the names in a rule document. Kept at exactly the shape
/// <see cref="SpecRegistry"/> already offers, so binders resolve a name to a
/// <see cref="SpecRegistryEntry"/> and never learn whether the entry was compiled into the
/// application or authored at runtime.
/// </summary>
/// <remarks>
/// Collections are host-registered in compiled code and have no runtime counterpart, so a layered
/// source resolves <see cref="FindCollection{TParent}"/> straight through to the registry.
/// </remarks>
internal interface ISpecSource
{
    /// <summary>Resolves a spec reference, or null when the name is unknown.</summary>
    SpecRegistryEntry? Find(string name);

    /// <summary>Resolves the collection registered for <typeparamref name="TParent"/> at a path, or null.</summary>
    CollectionBinding<TParent>? FindCollection<TParent>(string path);
}
