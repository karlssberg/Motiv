using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Deduplicates clauses based on their transformed expression and resolves composition expressions
///     to use camelCase variable names.
/// </summary>
public class ClauseSet
{
    private readonly Dictionary<int, string> _clauseNameMapping;

    public ClauseSet(
        IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)> clauses)
    {
        var uniqueClauses = new Dictionary<string, (string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression, string DerivedName)>();
        _clauseNameMapping = new Dictionary<int, string>();

        for (var i = 0; i < clauses.Count; i++)
        {
            var (original, transformedExpression, originalExpression) = clauses[i];
            var transformedKey = transformedExpression.ToString();

            if (!uniqueClauses.TryGetValue(transformedKey, out var clause))
            {
                var derivedName = ClauseNameDeriver.DeriveName(originalExpression, uniqueClauses.Count + 1);
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

    public IReadOnlyDictionary<string, (string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression, string
        DerivedName)> UniqueClauses { get; }

    /// <summary>
    ///     Resolves a composition expression by replacing clause identifier references with camelCase variable names.
    /// </summary>
    public ExpressionSyntax ResolveComposition(ExpressionSyntax compositionExpression)
    {
        var replacements = new Dictionary<string, string>();
        for (var i = 0; i < _clauseNameMapping.Count; i++)
        {
            var originalClauseName = $"Clause{i + 1}";
            var pascalCaseName = _clauseNameMapping[i];
            var camelCaseName = pascalCaseName.ToCamelCase();
            replacements[originalClauseName] = camelCaseName;
            replacements[pascalCaseName] = camelCaseName;
        }

        return compositionExpression.ReplaceNodes(
            compositionExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>(),
            (original, _) =>
                replacements.TryGetValue(original.Identifier.Text, out var camelName)
                    ? IdentifierName(camelName)
                    : original);
    }
}
