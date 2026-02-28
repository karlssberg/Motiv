using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Deduplicates clauses based on their transformed text and resolves composition expressions
///     to use camelCase variable names.
/// </summary>
public class ClauseSet
{
    private readonly Dictionary<int, string> _clauseNameMapping;

    public ClauseSet(
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses)
    {
        var uniqueClauses = new Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string DerivedName)>();
        _clauseNameMapping = new Dictionary<int, string>();

        for (var i = 0; i < clauses.Count; i++)
        {
            var (original, transformed, expression) = clauses[i];

            if (!uniqueClauses.TryGetValue(transformed, out var clause))
            {
                var derivedName = ClauseNameDeriver.DeriveName(expression, uniqueClauses.Count + 1);
                uniqueClauses[transformed] = (original, transformed, expression, derivedName);
                _clauseNameMapping[i] = derivedName;
            }
            else
            {
                _clauseNameMapping[i] = clause.DerivedName;
            }
        }

        UniqueClauses = uniqueClauses;
    }

    public IReadOnlyDictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string
        DerivedName)> UniqueClauses { get; }

    /// <summary>
    ///     Resolves a composition expression by replacing clause references with camelCase variable names.
    /// </summary>
    public string ResolveComposition(string compositionExpression)
    {
        var result = compositionExpression;

        for (var i = 0; i < _clauseNameMapping.Count; i++)
        {
            var originalClauseName = $"Clause{i + 1}";
            var pascalCaseName = _clauseNameMapping[i];
            var camelCaseName = pascalCaseName.ToCamelCase();

            result = result.Replace(originalClauseName, camelCaseName);
            result = result.Replace(pascalCaseName, camelCaseName);
        }

        return result;
    }
}
