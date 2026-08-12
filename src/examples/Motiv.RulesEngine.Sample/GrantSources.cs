using System.Security.Claims;
using System.Text.Json;
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

/// <summary>A single persisted grant row. <see cref="Verb"/> is one of "read"/"author"/"publish"
/// (the namespace-grant ladder) or "administer" (a subject-wide capability, not namespace-scoped —
/// its <see cref="Prefix"/> is ignored).</summary>
internal sealed record GrantRecord(string Subject, string Prefix, string Verb);

/// <summary>The outcome of <see cref="JsonFileGrantSource.Remove"/>.</summary>
internal enum GrantRemovalOutcome { Removed, NotFound, LastAdminister }

/// <summary>
/// The app-owned twin of <see cref="DevGrantSource"/>: a mutable, file-backed grant store subjects
/// can administer at runtime. Persistence mirrors <see cref="JsonFilePropositionStore"/> — same
/// JSON options, same "unreadable file becomes empty, never silently" handling — but the access
/// pattern differs: grants are loaded once into memory and cached, with every mutation rewriting
/// the whole file while holding the lock, rather than re-reading on every call. Grant administration
/// is a much hotter path than proposition authoring (checked on every gated request via
/// <c>GrantsFor</c>/<c>IsAdministrator</c>), so paying the read cost once at startup is worth it.
/// </summary>
/// <remarks>
/// Enforces the last-administer invariant: <see cref="Remove"/> refuses to drop the store's final
/// "administer" row, the grant-lockout twin of the gate-lockout guard elsewhere in the system. A
/// store can therefore never be mutated into a state with zero administrators.
/// </remarks>
internal sealed class JsonFileGrantSource(string path) : IGrantSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();
    private readonly List<GrantRecord> _grants = LoadFrom(path);

    public bool SupportsAdministration => true;

    /// <summary>Empty: app grants bind subjects directly rather than through roles.</summary>
    public IReadOnlyCollection<string> KnownRoles => [];

    /// <summary>A snapshot of every persisted grant row, administer rows included.</summary>
    public IReadOnlyList<GrantRecord> All
    {
        get { lock (_gate) return [.. _grants]; }
    }

    /// <summary>Whether the store currently has at least one "administer" row, for any subject.</summary>
    public bool AnyAdministrators
    {
        get { lock (_gate) return _grants.Exists(IsAdministerRow); }
    }

    /// <summary>Adds a grant and persists it. Throws for a <paramref name="grant"/> whose Verb is
    /// not one of "read"/"author"/"publish"/"administer" — an unknown verb fails loud, not silent.</summary>
    public void Add(GrantRecord grant)
    {
        ValidateVerb(grant.Verb);
        lock (_gate)
        {
            _grants.Add(grant);
            Write();
        }
    }

    /// <summary>Removes a grant and persists the change, unless <paramref name="grant"/> is the
    /// store's last remaining "administer" row — that removal is refused so the store can never end
    /// up with zero administrators.</summary>
    public GrantRemovalOutcome Remove(GrantRecord grant)
    {
        lock (_gate)
        {
            var index = _grants.FindIndex(existing => existing == grant);
            if (index < 0)
                return GrantRemovalOutcome.NotFound;

            if (IsAdministerRow(grant) && _grants.Count(IsAdministerRow) == 1)
                return GrantRemovalOutcome.LastAdminister;

            _grants.RemoveAt(index);
            Write();
            return GrantRemovalOutcome.Removed;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal)
    {
        var subject = Subject(principal);
        lock (_gate)
        {
            return [.. _grants
                .Where(grant => grant.Subject == subject && !IsAdministerRow(grant))
                .Select(grant => new NamespaceGrant(grant.Prefix, LadderVerb(grant.Verb)))];
        }
    }

    /// <inheritdoc />
    public bool IsAdministrator(ClaimsPrincipal principal)
    {
        var subject = Subject(principal);
        lock (_gate)
            return _grants.Exists(grant => grant.Subject == subject && IsAdministerRow(grant));
    }

    private void Write() => File.WriteAllText(path, JsonSerializer.Serialize(_grants, Json));

    private static List<GrantRecord> LoadFrom(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<GrantRecord>>(File.ReadAllText(path), Json) ?? [];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Mirrors JsonFilePropositionStore: a hand-edited or half-written file must not stop
            // the app booting, but the read failure is never swallowed silently — the next write
            // will overwrite this file with whatever was read before the failure (nothing, here).
            Console.Error.WriteLine(
                $"[JsonFileGrantSource] Could not read '{path}': {exception.Message}\n" +
                "  Continuing with no stored grants. The next write will OVERWRITE this file — " +
                "copy it aside now if you intend to repair it.");
            return [];
        }
    }

    private static bool IsAdministerRow(GrantRecord grant) =>
        string.Equals(grant.Verb, "administer", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps a validated ladder verb string to <see cref="GrantVerb"/>. Never called for an
    /// "administer" row — those are excluded from <see cref="GrantsFor"/> before this runs.</summary>
    private static GrantVerb LadderVerb(string verb) => verb.ToLowerInvariant() switch
    {
        "read" => GrantVerb.Read,
        "author" => GrantVerb.Author,
        "publish" => GrantVerb.Publish,
        _ => throw new InvalidOperationException(
            $"Corrupt grant verb '{verb}' — Add() should have rejected this before it reached storage.")
    };

    private static void ValidateVerb(string verb)
    {
        switch (verb.ToLowerInvariant())
        {
            case "read" or "author" or "publish" or "administer":
                return;
            default:
                throw new ArgumentException($"Unknown grant verb '{verb}'.", nameof(verb));
        }
    }

    /// <summary>The principal's stable subject: NameIdentifier, then "sub", then Name, then
    /// "unknown" — mirrors <c>Motiv.Serialization.AspNetCore.PrincipalIdentity.Subject</c>, which is
    /// internal to that assembly and not visible here.</summary>
    private static string Subject(ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? principal.FindFirst("sub")?.Value
        ?? principal.Identity?.Name
        ?? "unknown";
}

/// <summary>A claims-to-grant mapping: the claim type and value to watch for, and the namespace grant
/// to issue when a principal holds that claim. Verb is one of "read"/"author"/"publish" (the
/// namespace-grant ladder) or "administer" (a subject-wide capability, not namespace-scoped).</summary>
internal sealed record ClaimsGrantMapping(string ClaimType, string ClaimValue, string Prefix, string Verb);

/// <summary>
/// Maps IdP group/role claims to namespace grants via app config — the IdP does not know Motiv's
/// namespaces, so the mapping lives here. Administered in the IdP, so no in-app administration surface.
/// </summary>
internal sealed class ClaimsGrantSource(IReadOnlyList<ClaimsGrantMapping> mappings) : IGrantSource
{
    private readonly List<ClaimsGrantMapping> _validatedMappings = ValidateAndBuild(mappings);

    public bool SupportsAdministration => false;

    public IReadOnlyCollection<string> KnownRoles =>
        _validatedMappings.Select(m => m.ClaimValue).Distinct().ToList();

    /// <inheritdoc />
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        var grants = new List<NamespaceGrant>();

        foreach (var mapping in _validatedMappings)
        {
            if (roles.Contains(mapping.ClaimValue) && !IsAdministerRow(mapping))
            {
                grants.Add(new NamespaceGrant(mapping.Prefix, LadderVerb(mapping.Verb)));
            }
        }

        return grants;
    }

    /// <inheritdoc />
    public bool IsAdministrator(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        return _validatedMappings.Exists(m => roles.Contains(m.ClaimValue) && IsAdministerRow(m));
    }

    private static List<ClaimsGrantMapping> ValidateAndBuild(IReadOnlyList<ClaimsGrantMapping> mappings)
    {
        var result = new List<ClaimsGrantMapping>();
        foreach (var mapping in mappings)
        {
            ValidateVerb(mapping.Verb);
            // Normalize "role" (case-insensitive) to ClaimTypes.Role
            var claimType = string.Equals(mapping.ClaimType, "role", StringComparison.OrdinalIgnoreCase)
                ? ClaimTypes.Role
                : mapping.ClaimType;
            result.Add(new ClaimsGrantMapping(claimType, mapping.ClaimValue, mapping.Prefix, mapping.Verb));
        }
        return result;
    }

    private static bool IsAdministerRow(ClaimsGrantMapping mapping) =>
        string.Equals(mapping.Verb, "administer", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps a validated ladder verb string to <see cref="GrantVerb"/>.</summary>
    private static GrantVerb LadderVerb(string verb) => verb.ToLowerInvariant() switch
    {
        "read" => GrantVerb.Read,
        "author" => GrantVerb.Author,
        "publish" => GrantVerb.Publish,
        _ => throw new InvalidOperationException(
            $"Corrupt grant verb '{verb}' — Validation should have rejected this.")
    };

    private static void ValidateVerb(string verb)
    {
        switch (verb.ToLowerInvariant())
        {
            case "read" or "author" or "publish" or "administer":
                return;
            default:
                throw new ArgumentException($"Unknown grant verb '{verb}'.", nameof(verb));
        }
    }
}
