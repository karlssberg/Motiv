namespace Motiv.Serialization;

/// <summary>The 3am escape: a deploy-time flag (env/appsettings — requires ops access, never an
/// in-app toggle) that disables the gate while active. An infra-layer privilege above any in-app
/// grant. Loud, audited, and time-boxable so a forgotten break-glass auto-expires.</summary>
/// <param name="Enabled">Whether break-glass is configured on at all. False is the default everywhere.</param>
/// <param name="ExpiresUtc">
/// When the window closes, or null for no expiry. A deliberate omission, not a recommendation —
/// callers are expected to set this so a break-glass left on is not permanent.
/// </param>
public sealed record BreakGlass(bool Enabled, DateTimeOffset? ExpiresUtc)
{
    /// <summary>The default registration: break-glass off, gate always evaluated.</summary>
    public static readonly BreakGlass Off = new(false, null);

    /// <summary>Whether break-glass is bypassing the gate at <paramref name="nowUtc"/>.</summary>
    /// <param name="nowUtc">The current time; passed in rather than read internally so evaluation is testable.</param>
    /// <returns><see langword="true"/> when enabled and either unexpiring or not yet expired.</returns>
    public bool Active(DateTimeOffset nowUtc) => Enabled && (ExpiresUtc is null || nowUtc < ExpiresUtc);
}
