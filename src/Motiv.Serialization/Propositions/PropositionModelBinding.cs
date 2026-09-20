namespace Motiv.Serialization;

/// <summary>
/// Binds a proposition document for one model type. Written as a delegate so
/// <c>TModel</c> is captured at registration and binding needs no reflection — the same approach
/// the endpoints' model bindings already take.
/// </summary>
internal delegate SpecRegistryEntry? BindProposition(
    ISpecSource source,
    string name,
    string? description,
    RuleDocument document,
    bool isAsync,
    List<RuleError> errors);

/// <summary>A registered evaluable model type, with its binder closure.</summary>
internal sealed class PropositionModelBinding
{
    public required string Id { get; init; }

    public required Type ModelType { get; init; }

    public required BindProposition Bind { get; init; }

    /// <summary>
    /// The binding for <typeparamref name="TModel"/> under <paramref name="id"/>: what
    /// <see cref="PropositionSet.AddModel{TModel}"/> registers, and what a snapshot builds for
    /// itself to bind pinned documents without claiming a scope.
    /// </summary>
    public static PropositionModelBinding For<TModel>(string id, RuleSerializerOptions options) => new()
    {
        Id = id,
        ModelType = typeof(TModel),
        Bind = (source, name, description, document, isAsync, errors) =>
        {
            // Carried on the entry, so the next document to reference this proposition by name is
            // bounded against the depth it actually composes rather than scoring the reference as a
            // leaf — which is what let a catalogue compose past the stack ceiling one publish at a
            // time (#201).
            //
            // The binder walks the document again for its own cap check, and deliberately so:
            // handing the measure back out would mean an `out` parameter on all four Bind methods
            // and a discard at every RuleSerializer call site, to save one walk of a document
            // already bounded by MaxNodeCount at the one site that wants it.
            var depth = CompositionDepth.Of(document, source);

            if (isAsync)
            {
                var asyncSpec = AsyncRuleBinder.Bind<TModel>(document, source, options, errors);
                return asyncSpec is null
                    ? null
                    : new SpecRegistryEntry(name, typeof(TModel), typeof(string), true, asyncSpec, description, depth: depth);
            }

            var spec = RuleBinder.Bind<TModel>(document, source, options, errors);
            return spec is null
                ? null
                : new SpecRegistryEntry(name, typeof(TModel), typeof(string), false, spec, description, depth: depth);
        },
    };
}
