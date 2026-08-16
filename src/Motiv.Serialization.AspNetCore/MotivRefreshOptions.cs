namespace Motiv.Serialization.AspNetCore;

/// <summary>How often this replica checks whether another one has published.</summary>
/// <remarks>
/// The interval bounds how long two replicas can disagree, and is also the window in which a
/// cross-process write can be lost — see ticket 21's note that the version primary key closes the
/// lost-update hole, not the visibility one. Shorter is fresher and costs one scalar read per replica
/// per tick.
/// </remarks>
public sealed class MotivRefreshOptions
{
    /// <summary>How long to wait between polls. Defaults to five seconds.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
}
