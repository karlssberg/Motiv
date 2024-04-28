namespace Karlssberg.Motiv;

/// <summary>
/// Represents a specification description.
/// </summary>
public interface ISpecDescription
{
    /// <summary>
    /// Gets the statement of the specification.
    /// </summary>
    string Statement { get; }

    /// <summary>
    /// Gets the multi-lined serialization of the expression.
    /// </summary>
    string Detailed { get; }

    /// <summary>
    /// Retrieves the details of the specification as a collection of lines.
    /// </summary>
    /// <returns>An enumerable collection of strings, each representing a line of detail.</returns>
    internal IEnumerable<string> GetDetailsAsLines();
}