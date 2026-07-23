using System.Reflection;
using System.Runtime.CompilerServices;

namespace Motiv.Serialization;

/// <summary>A rule-document JSON payload used as a rule's default implementation.</summary>
/// <param name="Json">The rule-document JSON.</param>
public sealed record RuleDocumentSource(string Json)
{
    /// <summary>The rule-document JSON.</summary>
    public string Json { get; } = Json;
}

/// <summary>Sources for rule-document defaults.</summary>
public static class RuleDocuments
{
    /// <summary>Wraps raw rule-document JSON.</summary>
    /// <param name="json">The rule-document JSON.</param>
    /// <returns>A document source for a rule constructor.</returns>
    public static RuleDocumentSource FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("A rule document must not be empty or whitespace.", nameof(json));
        return new RuleDocumentSource(json);
    }

    /// <summary>
    /// Reads a rule document embedded in the calling assembly. The name matches by trailing
    /// resource-name segment, so a project-relative file name (e.g. <c>"loyalty.json"</c> or
    /// <c>"Rules/loyalty.json"</c>) resolves without the assembly-name prefix.
    /// </summary>
    /// <param name="resourceName">The embedded resource name or trailing path segment.</param>
    /// <returns>A document source for a rule constructor.</returns>
    /// <exception cref="InvalidOperationException">No unique matching resource exists.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static RuleDocumentSource Embedded(string resourceName) =>
        Embedded(resourceName, Assembly.GetCallingAssembly());

    /// <summary>Reads a rule document embedded in the given assembly.</summary>
    /// <param name="resourceName">The embedded resource name or trailing path segment.</param>
    /// <param name="assembly">The assembly holding the resource.</param>
    /// <returns>A document source for a rule constructor.</returns>
    /// <exception cref="InvalidOperationException">No unique matching resource exists.</exception>
    public static RuleDocumentSource Embedded(string resourceName, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException("A resource name must not be empty or whitespace.", nameof(resourceName));

        // Match whole trailing segments only — "loyalty.json" must not bind "e-loyalty.json".
        var suffix = resourceName.Replace('/', '.').Replace('\\', '.');
        var matches = assembly.GetManifestResourceNames()
            .Where(name => name.Equals(suffix, StringComparison.Ordinal)
                        || name.EndsWith("." + suffix, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
            throw new InvalidOperationException(matches.Length == 0
                ? $"No embedded resource ending in '{resourceName}' was found in {assembly.GetName().Name}."
                : $"Multiple embedded resources end in '{resourceName}' in {assembly.GetName().Name}: {string.Join(", ", matches)}.");

        using var stream = assembly.GetManifestResourceStream(matches[0])!;
        using var reader = new StreamReader(stream);
        return new RuleDocumentSource(reader.ReadToEnd());
    }
}
