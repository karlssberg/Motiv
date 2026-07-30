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
public sealed class SpecRegistry : ISpecSource
{
    private readonly Dictionary<string, SpecRegistryEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<(Type Parent, string Path), object> _collections = new();

    /// <summary>The number of registered specs.</summary>
    public int Count => _entries.Count;

    /// <summary>All registered entries. Intended for read-only catalog enumeration after population.</summary>
    public IReadOnlyCollection<SpecRegistryEntry> Entries => _entries.Values;

    /// <summary>All registered collection projections. Intended for read-only enumeration after population.</summary>
    public IReadOnlyCollection<CollectionRegistryEntry> Collections =>
        _collections
            .Select(kv => new CollectionRegistryEntry(kv.Key.Parent, kv.Key.Path, ((ICollectionBindingInfo)kv.Value).ElementType))
            .ToArray();

    /// <summary>Registers a synchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">
    /// The stable name that rule documents use to reference the spec. Must start with an ASCII letter
    /// and contain only ASCII letters, digits, <c>-</c> or <c>_</c>, so it can be referenced safely
    /// from documents and DSL text.
    /// </param>
    /// <param name="spec">The spec to register.</param>
    /// <param name="description">An optional human-readable description surfaced in a catalog UI.</param>
    /// <returns>This registry, to allow chained registration.</returns>
    public SpecRegistry Register<TModel, TMetadata>(string name, SpecBase<TModel, TMetadata> spec, string? description = null) =>
        Add(name, spec, typeof(TModel), typeof(TMetadata), isAsync: false, description);

    /// <summary>Registers an asynchronous spec under the given name.</summary>
    /// <typeparam name="TModel">The model type the spec evaluates against.</typeparam>
    /// <typeparam name="TMetadata">The metadata type the spec yields.</typeparam>
    /// <param name="name">
    /// The stable name that rule documents use to reference the spec. Must start with an ASCII letter
    /// and contain only ASCII letters, digits, <c>-</c> or <c>_</c>, so it can be referenced safely
    /// from documents and DSL text.
    /// </param>
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

    /// <summary>
    /// Whether a name is a legal spec reference: dot-separated segments, each an ASCII letter
    /// followed by ASCII letters, digits, <c>-</c> or <c>_</c>. Exposed so runtime authoring can
    /// reject a name by the same rule documents are bound by, rather than a second copy of it.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><c>true</c> when the name may be registered and referenced.</returns>
    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && IsIdentifierLike(name);

    /// <summary>
    /// Registers a projection from <typeparamref name="TParent"/> to a collection of
    /// <typeparamref name="TElement"/>, referenced by higher-order rule nodes via their <c>path</c>.
    /// </summary>
    public SpecRegistry RegisterCollection<TParent, TElement>(
        string path, Func<TParent, IEnumerable<TElement>> selector)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A collection path must not be empty or whitespace.", nameof(path));
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));

        var key = (typeof(TParent), path);
        if (_collections.ContainsKey(key))
            throw new ArgumentException(
                $"A collection is already registered at path '{path}' for model '{typeof(TParent).Name}'.", nameof(path));

        _collections[key] = new CollectionBinding<TParent, TElement>(selector);
        return this;
    }

    /// <summary>Resolves the collection registered for <typeparamref name="TParent"/> at a path, or null.</summary>
    internal CollectionBinding<TParent>? FindCollection<TParent>(string path) =>
        _collections.TryGetValue((typeof(TParent), path), out var binding)
            ? (CollectionBinding<TParent>)binding
            : null;

    /// <summary>
    /// Explicit implementation forwarding to the internal <see cref="FindCollection{TParent}"/>: an
    /// internal interface member cannot be satisfied implicitly by a non-public method, but the
    /// internal method must remain directly callable on <see cref="SpecRegistry"/> for existing callers.
    /// </summary>
    CollectionBinding<TParent>? ISpecSource.FindCollection<TParent>(string path) => FindCollection<TParent>(path);

    private SpecRegistry Add(string name, object? spec, Type modelType, Type metadataType, bool isAsync, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A registered spec name must not be empty or whitespace.", nameof(name));
        if (!IsValidName(name))
            throw new ArgumentException(
                $"The spec name '{name}' is not a valid identifier: names are referenced from rule " +
                "documents and DSL text, so each dot-separated segment must start with an ASCII " +
                "letter and contain only ASCII letters, digits, '-' or '_'.", nameof(name));
        if (spec is null)
            throw new ArgumentNullException(nameof(spec));
        if (_entries.ContainsKey(name))
            throw new ArgumentException($"A spec is already registered under the name '{name}'.", nameof(name));

        _entries[name] = new SpecRegistryEntry(name, modelType, metadataType, isAsync, spec, description);
        return this;
    }

    /// <summary>
    /// Dot-separated segments, each an ASCII letter followed by ASCII letters, digits, '-' or '_'.
    /// The dots namespace a name for tree presentation; no leading, trailing or doubled dot.
    /// </summary>
    private static bool IsIdentifierLike(string name)
    {
        var segmentStart = true;

        foreach (var character in name)
        {
            if (character == '.')
            {
                // A dot directly after another dot (or at the very start) leaves no segment between them.
                if (segmentStart)
                    return false;
                segmentStart = true;
                continue;
            }

            if (segmentStart && !IsAsciiLetter(character))
                return false;

            if (!IsAsciiLetter(character) && character is not ((>= '0' and <= '9') or '-' or '_'))
                return false;

            segmentStart = false;
        }

        // Still expecting a segment means the name ended on a dot (or was empty).
        return !segmentStart;
    }

    private static bool IsAsciiLetter(char character) =>
        character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
}
