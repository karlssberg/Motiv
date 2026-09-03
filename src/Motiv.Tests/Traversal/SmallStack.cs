using System.Runtime.ExceptionServices;

namespace Motiv.Tests.Traversal;

/// <summary>
/// The thread every depth suite in this folder measures on. Three of them built it by hand until this
/// existed, identically — the ceilings they record are only meaningful against a fixed stack size, so
/// the thread is the measurement rather than a detail of it.
/// </summary>
internal static class SmallStack
{
    /// <summary>
    /// The ASP.NET request-thread stack, and the size at which every ceiling recorded in this folder
    /// was measured. On the 8 MB main stack a depth case passes whether or not the recursion it guards
    /// was ever removed.
    /// </summary>
    internal const int Bytes = 1024 * 1024;

    /// <summary>
    /// Runs <paramref name="body" /> on a fresh <see cref="Bytes" />-byte thread and rethrows whatever
    /// it threw on the caller's, original stack trace intact. A stack overflow is not among those — it
    /// aborts the process rather than throwing, which is why the ceilings these suites sit under had to
    /// be bisected out-of-process.
    /// </summary>
    internal static void OnASmallStack(Action body)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    body();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            },
            Bytes) { IsBackground = true };

        thread.Start();
        thread.Join();

        failure?.Throw();
    }
}
