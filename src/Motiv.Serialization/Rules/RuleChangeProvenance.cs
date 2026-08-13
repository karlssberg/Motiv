using System.Reflection;

namespace Motiv.Serialization;

/// <summary>
/// The who/why of a publish, supplied by the caller and written into the version log. Carried as one
/// parameter rather than four so that adding an anchor later does not re-break every write signature.
/// </summary>
/// <param name="Author">Who is publishing. Required — an unattributed row is not an audit record.</param>
/// <param name="ChangeNote">An optional human-supplied reason.</param>
/// <param name="ApprovalRef">The change request this discharges, when the publish was governed.</param>
/// <param name="BuildId">
/// The build to pin, or null to take <see cref="BuildIdentity.Current"/> at write time.
/// </param>
public sealed record RuleChangeProvenance(
    string Author,
    string? ChangeNote = null,
    string? ApprovalRef = null,
    string? BuildId = null)
{
    /// <summary>
    /// The attribution for a publish no principal asked for — a startup load, or a rebind triggered by
    /// someone else's proposition edit. Distinguishable in the log from a person's edit, which is the
    /// point: "who changed this?" must not answer with the last human to touch something adjacent.
    /// </summary>
    public static RuleChangeProvenance System { get; } = new("system");

    /// <summary>Fills in anything the caller left to the library. Called once, at write time.</summary>
    public RuleChangeProvenance WithDefaults() =>
        BuildId is null ? this with { BuildId = BuildIdentity.Current } : this;
}

/// <summary>Identifies the running build, so a version row can pin behaviour that is not in a document.</summary>
public static class BuildIdentity
{
    /// <summary>
    /// The entry assembly's informational version, falling back to its plain version and then to
    /// <c>"unknown"</c>. Read once — it cannot change within a process, and a host that wants
    /// something more precise (a commit sha) passes <see cref="RuleChangeProvenance.BuildId"/> itself.
    /// </summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
            return "unknown";

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informational!;
    }
}
