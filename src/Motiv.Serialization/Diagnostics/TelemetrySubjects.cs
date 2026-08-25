using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Motiv.Serialization;

/// <summary>
/// The live instances an observable instrument reports from.
/// </summary>
/// <remarks>
/// <para>
/// Most of the <c>motiv.rules.*</c> instruments are readings rather than events — how deep the
/// decision queue is, which generation this replica serves, whether break-glass is on. A reading has
/// no call site to push from, so the instrument has to be handed the thing that holds it.
/// </para>
/// <para>
/// Held weakly, and that is the point rather than a precaution: a registry of strong references
/// would keep every <c>RuleSet</c> a process ever built alive for as long as the meter exists, so
/// merely subscribing to the meter would turn a garbage-collectable object into a leak. A subject
/// that goes away simply stops being reported, which is the correct reading — and
/// <see cref="Add"/>'s handle exists so a subject that knows when it is finished (a disposed
/// decision log) can say so immediately rather than wait for a collection.
/// </para>
/// </remarks>
/// <typeparam name="T">The kind of subject observed.</typeparam>
internal sealed class TelemetrySubjects<T> where T : class
{
    private readonly ConcurrentDictionary<long, WeakReference<T>> _live = new();
    private long _next;

    /// <summary>Registers <paramref name="subject"/> until the handle is disposed or it is collected.</summary>
    /// <param name="subject">The instance to report from.</param>
    /// <returns>A handle that unregisters it.</returns>
    public IDisposable Add(T subject)
    {
        var id = Interlocked.Increment(ref _next);
        _live[id] = new WeakReference<T>(subject);
        return new Registration(this, id);
    }

    /// <summary>
    /// Reads every live subject, pruning any that have been collected on the way past.
    /// </summary>
    /// <param name="read">What to report from one subject.</param>
    /// <returns>The measurements, ready to hand back to the observable instrument's callback.</returns>
    public IEnumerable<Measurement<long>> Observe(Func<T, IEnumerable<Measurement<long>>> read)
    {
        var measurements = new List<Measurement<long>>();

        foreach (var entry in _live)
        {
            if (!entry.Value.TryGetTarget(out var subject))
            {
                _live.TryRemove(entry.Key, out _);
                continue;
            }

            measurements.AddRange(read(subject));
        }

        return measurements;
    }

    private sealed class Registration(TelemetrySubjects<T> subjects, long id) : IDisposable
    {
        public void Dispose() => subjects._live.TryRemove(id, out _);
    }
}
