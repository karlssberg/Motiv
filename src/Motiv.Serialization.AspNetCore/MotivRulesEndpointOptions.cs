namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Per-mount endpoint options. The endpoints are secure by default; opening them requires the
/// explicit, greppable <see cref="AllowAnonymous"/> call at the mount site, so an open deployment
/// is auditable in review rather than the silent default.
/// </summary>
public sealed class MotivRulesEndpointOptions
{
    internal bool Anonymous { get; private set; }

    /// <summary>Opens every mapped endpoint to unauthenticated callers.</summary>
    public MotivRulesEndpointOptions AllowAnonymous()
    {
        Anonymous = true;
        return this;
    }
}
