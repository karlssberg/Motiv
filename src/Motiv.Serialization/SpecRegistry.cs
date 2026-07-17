namespace Motiv.Serialization;

/// <summary>
/// A catalog of specs registered under stable names. Names are the contract between compiled
/// code and externalized rule documents: documents reference and compose registered specs by name.
/// </summary>
/// <remarks>
/// Registration is intended to happen once at startup, before any documents are loaded. The
/// underlying dictionary is not synchronized: the registry is safe for concurrent reads (e.g.
/// concurrent <see cref="Find"/> calls) once population has finished, but <see cref="Register{TModel,TMetadata}(string,SpecBase{TModel,TMetadata})"/>
/// and its overload must not run concurrently with reads or with other registrations.
/// </remarks>
public sealed class SpecRegistry
{
    private readonly Dictionary<string, SpecRegistryEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>The number of registered specs.</summary>
    public int Count => _entries.Count;

    /// <summary>All registered entries. Intended for read-only catalog enumeration after population.</summary>
    public IReadOnlyCollection<SpecRegistryEntry> Entries => _entries.Values;

    /// <summary>Registers a synchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">The stable name that rule documents use to reference the spec.</param>
    /// <param name="spec">The spec to register.</param>
    /// <param name="description">An optional human-readable description surfaced in a catalog UI.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, SpecBase<TModel, TMetadata> spec, string? description = null) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: false, description);

    /// <summary>Registers an asynchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">The stable name that rule documents use to reference the spec.</param>
    /// <param name="spec">The spec to register.</param>
    /// <param name="description">An optional human-readable description surfaced in a catalog UI.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, AsyncSpecBase<TModel, TMetadata> spec, string? description = null) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: true, description);

    /// <summary>Looks up a registered spec by name.</summary>
    /// <param name="name">The name the spec was registered under.</param>
    /// <returns>The matching entry, or <c>null</c> when no spec is registered under the name.</returns>
    public SpecRegistryEntry? Find(string name) =>
        _entries.TryGetValue(name, out var entry) ? entry : null;

    private SpecRegistry Add(string name, object? spec, Type modelType, Type metadataType, bool isAsync, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A registered spec name must not be empty or whitespace.", nameof(name));
        if (spec is null)
            throw new ArgumentNullException(nameof(spec));
        if (_entries.ContainsKey(name))
            throw new ArgumentException($"A spec is already registered under the name '{name}'.", nameof(name));

        _entries[name] = new SpecRegistryEntry(name, modelType, metadataType, isAsync, spec, description);
        return this;
    }
}
