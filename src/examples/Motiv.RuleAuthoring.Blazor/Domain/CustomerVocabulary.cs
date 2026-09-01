using Motiv.Serialization;

namespace Motiv.RuleAuthoring.Blazor.Domain;

/// <summary>The propositions a document authored in this sample may reference.</summary>
/// <remarks>
/// The names deliberately match the ones used by <c>docs/adoption/index.md</c> and by Motiv Studio's
/// <c>loyalty-discount.json</c>, so a reader moving between them recognises the vocabulary.
/// </remarks>
public static class CustomerVocabulary
{
    /// <summary>Builds the registry the sample validates and binds against.</summary>
    /// <returns>The registry.</returns>
    public static SpecRegistry Registry() =>
        new SpecRegistry()
            .Register("customer.is-active", Spec.Build((Customer c) => c.IsActive).Create("is active"))
            .Register("customer.is-adult", Spec.Build((Customer c) => c.Age >= 18).Create("is adult"))
            .Register("customer.has-orders", Spec.Build((Customer c) => c.OrderCount > 0).Create("has orders"))
            .Register("customer.is-loyal", Spec.Build((Customer c) => c.OrderCount >= 3).Create("is loyal"));

    /// <summary>The registry names, ordered, for the editor's proposition picker.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [.. Registry().Entries.Select(entry => entry.Name).Order()];

    /// <summary>Customers the sample offers to evaluate an authored document against.</summary>
    public static IReadOnlyList<Customer> Samples { get; } =
    [
        new() { Name = "Ada", IsActive = true, Age = 36, OrderCount = 4 },
        new() { Name = "Bob", IsActive = true, Age = 17, OrderCount = 1 },
        new() { Name = "Cleo", IsActive = false, Age = 52, OrderCount = 0 }
    ];
}
