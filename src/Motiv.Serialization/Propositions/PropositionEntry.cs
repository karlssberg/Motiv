namespace Motiv.Serialization;

/// <summary>Where a proposition's current definition comes from.</summary>
public enum PropositionOrigin
{
    /// <summary>Compiled into the application; no authored document shadows it.</summary>
    Compiled,

    /// <summary>Compiled into the application, with an authored document currently shadowing it.</summary>
    Overridden,

    /// <summary>Authored at runtime, with no compiled counterpart.</summary>
    Authored
}

/// <summary>
/// One proposition as listed to a client: the effective definition plus where it came from.
/// </summary>
/// <param name="Name">The dot-separated name.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the effective definition evaluates asynchronously.</param>
/// <param name="Origin">Whether the definition is compiled, overridden, or authored.</param>
/// <param name="Version">The authored document's version, or 0 for a purely compiled proposition.</param>
/// <param name="Description">An optional human-readable description.</param>
/// <param name="Quarantine">
/// The binding errors that excluded an authored document from the effective set, or empty when it
/// bound. Quarantine is orthogonal to <see cref="Origin"/>, not a fourth value of it: an overridden
/// or an authored proposition can each be quarantined.
/// </param>
public sealed record PropositionEntry(
    string Name,
    string ModelType,
    string MetadataType,
    bool IsAsync,
    PropositionOrigin Origin,
    int Version,
    string? Description,
    IReadOnlyList<RuleError> Quarantine);
