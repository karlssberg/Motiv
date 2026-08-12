using System.Security.Claims;
using System.Text.Json;

namespace Motiv.RulesEngine.Sample;

/// <summary>
/// Keycloak nests realm roles under a <c>realm_access</c> claim (a JSON object with a
/// <c>roles</c> array) rather than surfacing them as individual role claims the way
/// <c>JwtBearer</c> and <see cref="ClaimsGrantSource"/>'s "role" claims-mapping expect.
/// <see cref="FlattenRealmRoles"/> bridges that gap by adding a <see cref="ClaimTypes.Role"/>
/// claim for each entry in <c>realm_access.roles</c>, wired into the sample's JwtBearer
/// <c>OnTokenValidated</c> event.
/// </summary>
internal static class KeycloakClaims
{
    /// <summary>
    /// Reads the principal's <c>realm_access</c> claim (if present) and adds a
    /// <see cref="ClaimTypes.Role"/> claim for every role it lists. A missing
    /// <c>realm_access</c> claim, or one that fails to parse as JSON, is a no-op — this never
    /// throws, since a malformed claim from an IdP should not fail token validation. Existing
    /// role claims (e.g. from other mappers) are left untouched; this only adds.
    /// </summary>
    public static void FlattenRealmRoles(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
            return;

        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
            return;

        try
        {
            using var document = JsonDocument.Parse(realmAccess);
            if (!document.RootElement.TryGetProperty("roles", out var roles)
                || roles.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var role in roles.EnumerateArray())
            {
                if (role.GetString() is { } roleName)
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
            }
        }
        catch (JsonException)
        {
            // Malformed realm_access claim — treat as absent rather than failing the request.
        }
    }
}
