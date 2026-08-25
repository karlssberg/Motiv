namespace Motiv.Serialization;

/// <summary>The 3am escape: a deploy-time flag (env/appsettings — requires ops access, never an
/// in-app toggle) that disables the gate while active. An infra-layer privilege above any in-app
/// grant. Loud, audited, and time-boxable so a forgotten break-glass auto-expires.</summary>
/// <remarks>
/// Written out longhand rather than as a positional record so the constructor can have a body, and
/// the body can register the instance with <c>motiv.rules.break_glass.active</c>. A positional
/// record's primary constructor takes no statements, and the registration needs <c>this</c> — so the
/// alternative was an <c>Enable telemetry</c> call every host had to remember, which is precisely the
/// silent gap a flag described as "loud" must not have. Construction, deconstruction, <c>with</c>
/// and value equality all behave as they did; the constructor's parameters are now spelled in
/// camelCase, as an ordinary constructor's are, which only affects a caller passing them by name.
/// </remarks>
public sealed record BreakGlass
{
    /// <summary>The default registration: break-glass off, gate always evaluated.</summary>
    public static readonly BreakGlass Off = new(false, null);

    /// <summary>Declares a break-glass window.</summary>
    /// <param name="enabled">Whether break-glass is configured on at all. False is the default everywhere.</param>
    /// <param name="expiresUtc">
    /// When the window closes, or null for no expiry. A deliberate omission, not a recommendation —
    /// callers are expected to set this so a break-glass left on is not permanent.
    /// </param>
    public BreakGlass(bool enabled, DateTimeOffset? expiresUtc)
    {
        Enabled = enabled;
        ExpiresUtc = expiresUtc;
        Report();
    }

    /// <summary>
    /// The copy constructor <c>with</c> uses. Written out so a derived window reports too — the
    /// compiler-generated one copies fields and would leave the clone invisible while the original it
    /// replaced went on being counted.
    /// </summary>
    private BreakGlass(BreakGlass original)
    {
        Enabled = original.Enabled;
        ExpiresUtc = original.ExpiresUtc;
        Report();
    }

    /// <summary>Whether break-glass is configured on at all. False is the default everywhere.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// When the window closes, or null for no expiry. A deliberate omission, not a recommendation —
    /// callers are expected to set this so a break-glass left on is not permanent.
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; init; }

    /// <summary>Whether break-glass is bypassing the gate at <paramref name="nowUtc"/>.</summary>
    /// <param name="nowUtc">The current time; passed in rather than read internally so evaluation is testable.</param>
    /// <returns><see langword="true"/> when enabled and either unexpiring or not yet expired.</returns>
    public bool Active(DateTimeOffset nowUtc) => Enabled && (ExpiresUtc is null || nowUtc < ExpiresUtc);

    /// <summary>Splits this window back into the pair it was declared from.</summary>
    /// <param name="enabled">Receives <see cref="Enabled"/>.</param>
    /// <param name="expiresUtc">Receives <see cref="ExpiresUtc"/>.</param>
    public void Deconstruct(out bool enabled, out DateTimeOffset? expiresUtc)
    {
        enabled = Enabled;
        expiresUtc = ExpiresUtc;
    }

    /// <summary>
    /// Reports this window through <c>motiv.rules.break_glass.active</c> for as long as something
    /// holds it — the registry keeps only a weak reference, so this is not a leak and needs no
    /// disposal point.
    /// </summary>
    /// <remarks>
    /// <see cref="Off"/> registers too, and that matters more than the enabled case: the gauge then
    /// reads 0 in an ordinary host instead of reporting nothing at all, so "no series" stops being
    /// ambiguous between break-glass being off and the replica's meter having stopped answering.
    /// An operator alerting on this needs those two to look different.
    /// </remarks>
    private void Report() => MotivRulesTelemetry.BreakGlasses.Add(this);
}
