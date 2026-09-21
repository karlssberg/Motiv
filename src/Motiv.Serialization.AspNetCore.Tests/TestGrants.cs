using System.Security.Claims;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>A grant source that answers the same grants for every principal.</summary>
internal sealed class FixedGrants(IReadOnlyList<NamespaceGrant> grants) : IGrantSource
{
    public bool SupportsAdministration => false;
    public IReadOnlyCollection<string> KnownRoles => [];
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) => grants;
    public bool IsAdministrator(ClaimsPrincipal principal) => false;
}

/// <summary>Grants that can be narrowed after the host has been set up under full rights.</summary>
internal sealed class SwappableGrants : IGrantSource
{
    public IReadOnlyList<NamespaceGrant> Grants { get; set; } =
        [new NamespaceGrant("", GrantVerb.Read), new NamespaceGrant("", GrantVerb.Author), new NamespaceGrant("", GrantVerb.Publish)];

    public bool SupportsAdministration => false;
    public IReadOnlyCollection<string> KnownRoles => [];
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) => Grants;
    public bool IsAdministrator(ClaimsPrincipal principal) => false;
}
