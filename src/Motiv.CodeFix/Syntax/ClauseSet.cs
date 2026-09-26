using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Deduplicates clauses based on their transformed expression, names each distinct clause uniquely, and
///     resolves composition expressions to use those camelCase variable names.
/// </summary>
public class ClauseSet
{
    private readonly Dictionary<int, string> _clauseNameMapping;

    /// <summary>
    ///     The identifier standing for the <paramref name="position" />th clause (1-based) in a composition expression.
    /// </summary>
    public static string Placeholder(int position) => $"Clause{position}";

    public ClauseSet(
        IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)> clauses)
    {
        var uniqueClauses = new Dictionary<string, (string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression, string DerivedName)>();
        var usedNames = new HashSet<string>();
        _clauseNameMapping = new Dictionary<int, string>();

        for (var i = 0; i < clauses.Count; i++)
        {
            var (original, transformedExpression, originalExpression) = clauses[i];
            var transformedKey = transformedExpression.ToString();

            if (!uniqueClauses.TryGetValue(transformedKey, out var clause))
            {
                var derivedName = Unique(ClauseNameDeriver.DeriveName(originalExpression, uniqueClauses.Count + 1), usedNames);
                uniqueClauses[transformedKey] = (original, transformedExpression, originalExpression, derivedName);
                _clauseNameMapping[i] = derivedName;
            }
            else
            {
                _clauseNameMapping[i] = clause.DerivedName;
            }
        }

        UniqueClauses = uniqueClauses;
    }

    private static string Unique(string name, HashSet<string> usedNames)
    {
        var candidate = name;
        for (var suffix = 2; !usedNames.Add(candidate); suffix++)
            candidate = $"{name}{suffix}";
        return candidate;
    }

    public IReadOnlyDictionary<string, (string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression, string
        DerivedName)> UniqueClauses { get; }

    /// <summary>
    ///     Resolves a composition expression by replacing clause identifier references with camelCase variable names.
    /// </summary>
    public ExpressionSyntax ResolveComposition(ExpressionSyntax compositionExpression)
    {
        var replacements = _clauseNameMapping.ToDictionary(
            mapping => Placeholder(mapping.Key + 1),
            mapping => mapping.Value.ToCamelCase());

        return compositionExpression.ReplaceNodes(
            compositionExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>(),
            (original, _) =>
                replacements.TryGetValue(original.Identifier.Text, out var camelName)
                    ? IdentifierName(camelName)
                    : original);
    }
}
