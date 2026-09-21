namespace Motiv.Serialization;

/// <summary>
/// How a rule document is printed as C#. <see cref="ModelType"/> is never guessed: it is the
/// rule's, or the registry entry's. Handles make the print read like hand-written code; without
/// them every reference resolves through <see cref="RegistryExpression"/> at run time.
/// </summary>
public sealed class CSharpPrintOptions
{
    /// <summary>The model type the printed method is over.</summary>
    public required Type ModelType { get; init; }

    /// <summary>The C# expression an unmapped spec reference resolves through; the printed method's first parameter.</summary>
    public string RegistryExpression { get; init; } = "registry";

    /// <summary>
    /// Spec name → the C# expression that is that spec (a compiled handle such as
    /// <c>Specs.IsActive</c>). Unmapped names go through <see cref="RegistryExpression"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> SpecHandles { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Collection path → its element type and selector, for higher-order nodes. An unmapped path prints a <c>TODO</c>.</summary>
    public IReadOnlyDictionary<string, CSharpCollectionHandle> Collections { get; init; } = new Dictionary<string, CSharpCollectionHandle>(StringComparer.Ordinal);

    /// <summary>The spec names registered as async; a reference to one prints through <c>GetAsync</c> and makes the method async-typed.</summary>
    public ISet<string> AsyncSpecs { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Every name the host's registry knows, or null not to check. A reference to a name outside
    /// it — a runtime-authored proposition, say — prints with a <c>TODO</c> and a warning, because
    /// <c>registry.Get</c> would throw for it the first time the adopted code ran.
    /// </summary>
    public IReadOnlyCollection<string>? KnownSpecs { get; init; }

    /// <summary>The namespace to emit, or null for none. Only used with <see cref="ClassName"/>.</summary>
    public string? Namespace { get; init; }

    /// <summary>The static class to wrap the method in, or null to print the method alone.</summary>
    public string? ClassName { get; init; }

    /// <summary>The printed method's name.</summary>
    public string MethodName { get; init; } = "Build";

    /// <summary>The options the document is parsed with, or null for defaults.</summary>
    public RuleSerializerOptions? SerializerOptions { get; init; }
}

/// <summary>What a higher-order node needs to print over a registered collection.</summary>
/// <param name="ElementType">The C# name of the element type (<c>int</c>, <c>Order</c>).</param>
/// <param name="Selector">The lambda from the model to the collection (<c>c =&gt; c.Orders</c>), or null to print a <c>TODO</c>.</param>
public sealed record CSharpCollectionHandle(string ElementType, string? Selector);

/// <summary>The printed source, and every place in it that needs a person's attention.</summary>
public sealed record CSharpPrintedRule(string Source, IReadOnlyList<string> Warnings);
