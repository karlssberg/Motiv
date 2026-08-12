namespace Motiv.Serialization;

/// <summary>
/// Describes a spec registered with a <see cref="SpecRegistry" />: its stable name, model and
/// metadata types, whether it evaluates asynchronously, and — when it was registered as
/// parameterised — the arguments a referencing node must supply.
/// </summary>
public sealed class SpecRegistryEntry
{
    internal SpecRegistryEntry(
        string name,
        Type modelType,
        Type metadataType,
        bool isAsync,
        object spec,
        string? description = null,
        IReadOnlyList<RuleParameterDeclaration>? parameters = null)
    {
        Name = name;
        ModelType = modelType;
        MetadataType = metadataType;
        IsAsync = isAsync;
        Spec = spec;
        Description = description;
        Parameters = parameters;
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

    /// <summary>
    /// The arguments a referencing node must supply, or <c>null</c> when the entry is a plain
    /// (non-parameterised) registration.
    /// </summary>
    internal IReadOnlyList<RuleParameterDeclaration>? Parameters { get; }

    /// <summary>
    /// The registered spec, model-erased — or, for a parameterised entry, the equally model-erased
    /// <c>Func&lt;IReadOnlyDictionary&lt;string, object?&gt;, object&gt;</c> factory that builds one
    /// from resolved arguments. Binders reach it through <see cref="ResolveSpec" /> rather than
    /// directly, so the two cases stay indistinguishable to them.
    /// </summary>
    internal object Spec { get; }

    /// <summary>
    /// Resolves the spec a node references: the registered instance for a plain entry, or the
    /// factory's product for a parameterised one, with the node's <c>args</c> validated and coerced
    /// against the declarations first.
    /// </summary>
    /// <returns>
    /// The model-erased spec, or <c>null</c> when an error was reported — the factory is never
    /// invoked with arguments that failed to resolve, so a caller's own null check is its error path.
    /// </returns>
    internal object? ResolveSpec(RuleNode node, List<RuleError> errors)
    {
        if (Parameters is null)
        {
            if (node.Args is null)
                return Spec;

            errors.Add(new RuleError(node.Path, RuleErrorCode.UnexpectedArguments,
                $"'{Name}' takes no arguments; it was not registered as a parameterised spec"));
            return null;
        }

        var errorCountBefore = errors.Count;
        var values = RuleParameterResolver.Resolve(Parameters, node.Args, errors, $"{node.Path}.args");
        if (errors.Count > errorCountBefore)
            return null;

        return ((Func<IReadOnlyDictionary<string, object?>, object>)Spec)(values);
    }
}
