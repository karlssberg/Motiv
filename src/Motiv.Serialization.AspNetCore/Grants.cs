using System.Security.Claims;

namespace Motiv.Serialization.AspNetCore;

/// <summary>The verb ladder: publish ⊃ author ⊃ read. Enum order is load-bearing.</summary>
public enum GrantVerb { Read, Author, Publish }

/// <summary>A namespace-scoped verb grant.</summary>
public sealed record NamespaceGrant(string Prefix, GrantVerb Verb);

/// <summary>Yields a principal's namespace grants. Swappable: app store, IdP claims, or dev.</summary>
public interface IGrantSource
{
    /// <summary>Whether grants can be administered in-app (mutable source). Gates the admin surface.</summary>
    bool SupportsAdministration { get; }

    /// <summary>The role universe for the lockout pre-check; empty when unknowable.</summary>
    IReadOnlyCollection<string> KnownRoles { get; }

    /// <summary>Yields the namespace grants for a principal.</summary>
    IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal);

    /// <summary>Whether the principal holds administer — gate config and grant administration.</summary>
    bool IsAdministrator(ClaimsPrincipal principal);
}

/// <summary>Evaluates namespace grants against required verbs.</summary>
public static class GrantEvaluator
{
    /// <summary>
    /// Whether <paramref name="grants"/> satisfy <paramref name="verb"/> for <paramref name="name"/>.
    /// Applies the verb ladder: higher verbs cover lower ones.
    /// </summary>
    public static bool IsGranted(IReadOnlyList<NamespaceGrant> grants, GrantVerb verb, string name)
    {
        foreach (var grant in grants)
            if (grant.Verb >= verb && NamespacePrefix.Covers(grant.Prefix, name))
                return true;
        return false;
    }

    /// <summary>Whether <paramref name="grants"/> include Author or Publish on any namespace.</summary>
    public static bool CanAuthorAnywhere(IReadOnlyList<NamespaceGrant> grants)
    {
        foreach (var grant in grants)
            if (grant.Verb >= GrantVerb.Author)
                return true;
        return false;
    }
}
