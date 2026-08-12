using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Reads a principal's identity in the terms <see cref="IGrantSource"/> consumers expect.</summary>
internal static class PrincipalIdentity
{
    /// <summary>The principal's stable subject: NameIdentifier, then "sub", then Name, then "unknown".</summary>
    public static string Subject(ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? principal.FindFirst("sub")?.Value
        ?? principal.Identity?.Name
        ?? "unknown";

    /// <summary>The principal's distinct roles, from both <see cref="ClaimTypes.Role"/> and "roles" claims.</summary>
    public static IReadOnlyList<string> Roles(ClaimsPrincipal principal) =>
        [.. principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("roles"))
            .Select(claim => claim.Value).Distinct()];
}

/// <summary>
/// Enforces namespace grants at the endpoint boundary. Every check returns null to let the request
/// proceed, or a 403 <see cref="ErrorResponse"/> naming the missing verb/name. When no
/// <see cref="IGrantSource"/> is registered, every check returns null unconditionally — grants are
/// opt-in, so without a source the surface stays authenticated-only, the Phase 1 behaviour existing
/// callers depend on.
/// </summary>
internal static class GrantGate
{
    /// <summary>Refuses the request unless its grants include <paramref name="verb"/> on <paramref name="name"/>.</summary>
    public static IResult? Refuse(HttpContext http, GrantVerb verb, string name, JsonSerializerOptions json) =>
        RefuseUnless(
            http, json,
            source => GrantEvaluator.IsGranted(source.GrantsFor(http.User), verb, name),
            $"Requires the '{verb.ToString().ToLowerInvariant()}' grant on '{name}'.");

    /// <summary>Refuses the request unless its grants include Author or Publish on at least one namespace.</summary>
    public static IResult? RefuseUnlessAuthorAnywhere(HttpContext http, JsonSerializerOptions json) =>
        RefuseUnless(
            http, json,
            source => GrantEvaluator.CanAuthorAnywhere(source.GrantsFor(http.User)),
            "Requires an 'author' grant on at least one namespace.");

    /// <summary>Refuses the request unless the principal is an administrator.</summary>
    public static IResult? RefuseUnlessAdministrator(HttpContext http, JsonSerializerOptions json) =>
        RefuseUnless(
            http, json,
            source => source.IsAdministrator(http.User),
            "Requires 'administer'.");

    /// <summary>
    /// Shared shape behind every check above: no registered <see cref="IGrantSource"/> means the
    /// request proceeds unconditionally (grants are opt-in); otherwise <paramref name="isGranted"/>
    /// decides between proceeding and a 403 naming <paramref name="requirement"/>.
    /// </summary>
    private static IResult? RefuseUnless(
        HttpContext http, JsonSerializerOptions json, Func<IGrantSource, bool> isGranted, string requirement)
    {
        if (http.RequestServices.GetService<IGrantSource>() is not { } source)
            return null;
        return isGranted(source)
            ? null
            : Results.Json(new ErrorResponse(requirement), json, statusCode: 403);
    }
}
