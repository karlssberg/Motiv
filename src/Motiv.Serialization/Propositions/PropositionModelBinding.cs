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
}
