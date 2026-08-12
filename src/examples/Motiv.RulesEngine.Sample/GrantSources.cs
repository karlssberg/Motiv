using System.Security.Claims;
using Motiv.Serialization.AspNetCore;

namespace Motiv.RulesEngine.Sample;

/// <summary>
/// The authorization-side twin of the dev identity: grants the single dev principal everything,
/// zero-config, and evaporates the moment the switch is off — never persisted. Immutable, so it
/// has no administration surface (a leaked dev superuser cannot persist new grants), but the dev
/// principal IS the first administrator (ticket 14): gate configuration works out of the box.
/// </summary>
internal sealed class DevGrantSource : IGrantSource
{
    public bool SupportsAdministration => false;
    public IReadOnlyCollection<string> KnownRoles => ["motiv-dev"];
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) =>
        [new NamespaceGrant("", GrantVerb.Publish)];
    public bool IsAdministrator(ClaimsPrincipal principal) => true;
}
