namespace Motiv.Not;

/// <summary>
/// Renames a binary operator heading when its composition is negated: <c>AND</c> becomes <c>NAND</c>,
/// and <c>NAND</c> becomes <c>AND</c> again, so that negating twice restores the original heading.
/// </summary>
/// <remarks>
/// The lookup is a <see langword="switch" /> expression evaluated on every call rather than a
/// dictionary built once in a static initialiser. Mutation testing switches mutants at run time, and a
/// table populated before any mutant is active can never exhibit one, so every entry of such a table
/// survives regardless of how well it is tested.
/// </remarks>
internal static class OperatorNegation
{
    /// <summary>
    /// Returns the negated form of <paramref name="operation" />, or <see langword="null" /> when it is
    /// not a binary operator heading. Matching is exact and case-sensitive.
    /// </summary>
    internal static string? Negate(string operation) =>
        operation switch
        {
            "AND" => "NAND",
            "AND ALSO" => "NAND ALSO",
            "OR" => "NOR",
            "OR ELSE" => "NOR ELSE",
            "XOR" => "XNOR",
            "NAND" => "AND",
            "NAND ALSO" => "AND ALSO",
            "NOR" => "OR",
            "NOR ELSE" => "OR ELSE",
            "XNOR" => "XOR",
            _ => null
        };
}
