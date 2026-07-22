namespace Motiv.Serialization;

/// <summary>
/// The non-generic identity of a rule: a named, versioned, hot-swappable decision handle.
/// Concrete rules derive from <see cref="Rule{TModel,TMetadata}"/>.
/// </summary>
public abstract class RuleBase
{
    private protected RuleBase(string name, RuleDefault @default, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A rule name must not be empty or whitespace.", nameof(name));

        Name = name;
        Default = @default;
        Description = description;
    }

    /// <summary>The stable name the rule is addressed by (endpoints, RuleSet lookups).</summary>
    public string Name { get; }

    /// <summary>An optional human-readable description surfaced by the rules endpoints.</summary>
    public string? Description { get; }

    /// <summary>The model type the rule evaluates against.</summary>
    public abstract Type ModelType { get; }

    /// <summary>The metadata type the rule yields.</summary>
    public abstract Type MetadataType { get; }

    /// <summary>Whether the rule evaluates asynchronously.</summary>
    public abstract bool IsAsync { get; }

    /// <summary>Whether the rule is policy-flavoured (yields a single value).</summary>
    public abstract bool IsPolicy { get; }

    /// <summary>The current version, starting at 1 and incremented by every successful update or revert.</summary>
    public abstract int Version { get; }

    /// <summary>The current document JSON, or null while the rule is on a compiled default.</summary>
    public abstract string? DocumentJson { get; }

    internal RuleDefault Default { get; }

    /// <summary>Binds the default and publishes version 1. Called exactly once, by <see cref="RuleSet.Add"/>.</summary>
    internal abstract void Attach(RuleSerializer serializer);

    /// <summary>Validates and binds the document, then CAS-publishes it over <paramref name="expectedVersion"/>.</summary>
    internal abstract RuleUpdateResult TryUpdate(RuleSerializer serializer, string documentJson, int expectedVersion);

    /// <summary>CAS-publishes the default back over <paramref name="expectedVersion"/>, bumping the version.</summary>
    internal abstract RuleUpdateResult TryRevert(RuleSerializer serializer, int expectedVersion);

    /// <summary>Reads the version and document from one snapshot, so the pair is always coherent.</summary>
    internal abstract (int Version, string? DocumentJson) VersionedDocument();
}
