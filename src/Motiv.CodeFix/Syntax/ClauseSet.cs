using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Deduplicates clauses based on their transformed expression, names each distinct clause uniquely, and
///     resolves composition expressions to use those camelCase variable names.
/// </summary>
public class ClauseSet
{
    private readonly Dictionary<string, string> _placeholderReplacements;

    /// <summary>
    ///     The identifier standing for the <paramref name="position" />th clause (1-based) in a composition expression.
    /// </summary>
    public static string Placeholder(int position) => $"Clause{position}";

    /// <param name="clauses">The clauses, in the order the composition's placeholders number them.</param>
    /// <param name="reservedNames">Names in scope that no clause variable may take, such as the spec's constructor parameters.</param>
    public ClauseSet(
        IReadOnlyList<(string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression)> clauses,
        IEnumerable<string>? reservedNames = null)
    {
        var uniqueClauses = new Dictionary<string, (string OriginalText, ExpressionSyntax TransformedExpression, ExpressionSyntax OriginalExpression, string DerivedName)>();
        var reserved = new HashSet<string>((reservedNames ?? []).Select(name => name.TrimStart('@').Capitalize()));
        var usedNames = new HashSet<string>(reserved);
        _placeholderReplacements = new Dictionary<string, string>();

        for (var i = 0; i < clauses.Count; i++)
        {
            var (original, transformedExpression, originalExpression) = clauses[i];
            var transformedKey = transformedExpression.ToString();

            if (!uniqueClauses.TryGetValue(transformedKey, out var clause))
            {
                var derivedName = Unique(ClauseNameDeriver.DeriveName(originalExpression, uniqueClauses.Count + 1), originalExpression, usedNames, reserved);
                clause = (original, transformedExpression, originalExpression, derivedName);
                uniqueClauses[transformedKey] = clause;
            }

            // A repeated clause resolves to the name of its first occurrence
            _placeholderReplacements[Placeholder(i + 1)] = clause.DerivedName.ToCamelCase();
        }

        UniqueClauses = uniqueClauses;
    }

    /// <summary>
    ///     <paramref name="name" />, or when taken, a numbered one — except that a call named like a reserved name, as a
    ///     call to a delegate is, takes its arguments first: <c>hasRoomForQuantity</c> beside a <c>hasRoomFor</c> delegate.
    /// </summary>
    private static string Unique(string name, ExpressionSyntax clause, HashSet<string> usedNames, HashSet<string> reserved)
    {
        if (usedNames.Add(name))
            return name;

        if (reserved.Contains(name) && clause is InvocationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } invocation)
        {
            var withArguments = name + string.Concat(invocation.ArgumentList.Arguments
                .SelectMany(argument => argument.Expression.DescendantTokens())
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(token => token.ValueText.ToPascalCase()));
            if (usedNames.Add(withArguments))
                return withArguments;
        }

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
        return compositionExpression.ReplaceNodes(
            compositionExpression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>(),
            (original, _) =>
                _placeholderReplacements.TryGetValue(original.Identifier.Text, out var camelName)
                    ? IdentifierName(camelName)
                    : original);
    }
}
