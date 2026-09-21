namespace Motiv.Serialization;

/// <summary>
/// How a reference-only capture gets its model back for a reproduction: one resolver per model
/// type, registered at startup beside the capture posture. Nothing is registered by default — an
/// adopter who registers nothing gets scaffolds, never data — and null is the erasure case: the
/// subject is gone and replay is correctly impossible.
/// </summary>
public sealed class DecisionModelResolvers
{
    private readonly Dictionary<Type, Func<string, CancellationToken, Task<object?>>> _resolvers = new();

    /// <summary>
    /// Registers how a <typeparamref name="TModel"/> is fetched from the key a
    /// <see cref="DecisionInputKind.Reference"/> capture stored. Returning null means the subject no
    /// longer exists.
    /// </summary>
    public DecisionModelResolvers Reference<TModel>(Func<string, CancellationToken, Task<TModel?>> resolver)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        _resolvers[typeof(TModel)] = async (key, ct) => await resolver(key, ct).ConfigureAwait(false);
        return this;
    }

    /// <summary>Whether a resolver is registered for <paramref name="modelType"/>.</summary>
    internal bool Covers(Type modelType) => _resolvers.ContainsKey(modelType);

    /// <summary>The model for <paramref name="key"/>, or null when nothing is registered or the subject is gone.</summary>
    internal Task<object?> ResolveAsync(Type modelType, string key, CancellationToken cancellationToken) =>
        _resolvers.TryGetValue(modelType, out var resolver)
            ? resolver(key, cancellationToken)
            : Task.FromResult<object?>(null);
}
