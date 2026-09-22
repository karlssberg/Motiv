using Microsoft.AspNetCore.Hosting;

namespace Motiv.Studio.Tests;

/// <summary>Shared wiring for hosts built from <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
internal static class StudioHost
{
    /// <summary>
    /// Points this host's databases at fresh temp files rather than Studio's real
    /// <c>motiv-store.db</c> and <c>motiv-decisions.db</c>, which every
    /// <c>WebApplicationFactory&lt;Program&gt;</c> in this assembly — and <c>dotnet run</c> itself —
    /// otherwise shares on disk.
    /// </summary>
    /// <remarks>
    /// xunit runs test classes in parallel, so a shared store would let one class's published rules
    /// and grants show up in another's assertions, and a shared decision log would do the same with
    /// its decisions. The two are separate databases by design, so isolating a host means isolating
    /// both — which is exactly why this is one call rather than a pair of `UseSetting`s copied into
    /// nine files.
    /// </remarks>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The builder, to allow chained configuration.</returns>
    public static IWebHostBuilder UseIsolatedDatabases(this IWebHostBuilder builder) => builder
        .UseSetting("Motiv:Store:ConnectionString", TempDatabase("motiv"))
        .UseSetting("Motiv:Decisions:ConnectionString", TempDatabase("motiv-decisions"));

    private static string TempDatabase(string prefix) =>
        $"Data Source={Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.db")}";
}
