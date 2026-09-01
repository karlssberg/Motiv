namespace Motiv.RuleAuthoring.Blazor.Domain;

/// <summary>The model the sample's propositions are written against.</summary>
public sealed class Customer
{
    /// <summary>The customer's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the account is active.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The customer's age in years.</summary>
    public required int Age { get; init; }

    /// <summary>How many orders the customer has placed.</summary>
    public required int OrderCount { get; init; }
}
