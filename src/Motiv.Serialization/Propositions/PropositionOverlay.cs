namespace Motiv.Serialization;

/// <summary>
/// The authored layer of the layered source: names currently backed by an authored document.
/// </summary>
/// <remarks>
/// Copy-construction is how a publish stays atomic without partial mutation. A prospective overlay is
/// cloned, bound into freely, and either swapped in whole or discarded — so a failed publish cannot
/// leave half-applied entries behind. Publishes are rare, so cloning a dictionary is not a cost worth
/// optimising away.
/// </remarks>
internal sealed class PropositionOverlay : ISpecSource
{
    private readonly Dictionary<string, SpecRegistryEntry> _entries;

    public PropositionOverlay() => _entries = new Dictionary<string, SpecRegistryEntry>(StringComparer.Ordinal);

    public PropositionOverlay(PropositionOverlay copyFrom) =>
        _entries = new Dictionary<string, SpecRegistryEntry>(copyFrom._entries, StringComparer.Ordinal);

    public void Set(SpecRegistryEntry entry) => _entries[entry.Name] = entry;

    public void Remove(string name) => _entries.Remove(name);

    public SpecRegistryEntry? Find(string name) =>
        _entries.TryGetValue(name, out var entry) ? entry : null;

    // Collections are compiled-only; the layered source resolves them from the registry.
    public CollectionBinding<TParent>? FindCollection<TParent>(string path) => null;
}
