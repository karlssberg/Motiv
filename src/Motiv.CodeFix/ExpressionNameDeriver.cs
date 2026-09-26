using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Derives meaningful class names from boolean expression content.
/// </summary>
public static class ExpressionNameDeriver
{
    private const string ModelName = "Model";
    private const string PropositionBaseName = "Proposition";
    private const int MaxJoinedNameLength = 50;

    /// <summary>
    /// Derives Proposition and Model class names from an expression's content.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="insertionPosition">The position where the new classes will be inserted.</param>
    /// <returns>A tuple containing the Proposition name and Model name.</returns>
    public static (string PropositionName, string ModelName) DeriveClassNames(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        int insertionPosition)
    {
        // Step 1: Derive base name with precedence order:
        // 1. Assignment target name (highest priority)
        // 2. Method return context name
        // 3. Expression-based name
        // 4. Fallback "Proposition" (lowest priority)
        var baseName = DeriveBaseNameFromContext(expression, semanticModel);

        // Step 2: Convert to PascalCase, without a field's leading underscore
        var pascalName = baseName.TrimStart('_').Capitalize();

        // Step 3: Append suffixes (unless base name is already the fallback)
        var propositionName = pascalName is PropositionBaseName ? pascalName: $"{pascalName}{PropositionBaseName}";

        // Step 4: Ensure uniqueness
        propositionName = EnsureUniqueName(propositionName, semanticModel, insertionPosition);
        var modelName = EnsureUniqueName(ModelName, semanticModel, insertionPosition);

        return (propositionName, modelName);
    }

    /// <summary>
    /// Derives the base name with context awareness, following precedence order.
    /// </summary>
    private static string DeriveBaseNameFromContext(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // Priority 1: Check if expression is assigned to a variable
        if (TryGetAssignmentTargetName(expression, out var assignmentName))
        {
            return assignmentName;
        }

        // Priority 2: Check if expression is returned from a method
        if (TryGetMethodReturnContextName(expression, out var methodName))
        {
            return methodName;
        }

        // Priority 3: A condition or argument has nothing to borrow a name from, so name what it tests
        if (!HasReturnContext(expression) && TryGetClauseMeaningName(expression, out var clauseMeaningName))
        {
            return clauseMeaningName;
        }

        // Priority 4: Derive from expression content
        return DeriveBaseName(expression, semanticModel);
    }

    private static bool HasReturnContext(ExpressionSyntax expression) =>
        expression.Ancestors().Any(ancestor => ancestor is ReturnStatementSyntax or ArrowExpressionClauseSyntax);

    /// <summary>
    /// Names a condition after what it tests: one clause by its own meaning (<c>IsNPositive</c>), two joined by
    /// their operator with a shared subject stated once (<c>IsNPositiveAndLessThan10</c>). More than two clauses
    /// would make a sentence rather than a name, so the enclosing member names them instead.
    /// </summary>
    private static bool TryGetClauseMeaningName(ExpressionSyntax expression, out string name)
    {
        name = DescribeClauses(Unparenthesize(expression)) ?? EnclosingMemberName(expression) ?? string.Empty;
        return name.Length > 0;
    }

    private static string? DescribeClauses(ExpressionSyntax condition)
    {
        if (!ContainsLogicalOperator(condition))
            return ClauseMeaning(condition);

        if (condition is not BinaryExpressionSyntax binary
            || GetConnective(binary) is not { } connective
            || ContainsLogicalOperator(binary.Left)
            || ContainsLogicalOperator(binary.Right))
        {
            return null;
        }

        return JoinClauseMeanings(ClauseMeaning(binary.Left), connective, ClauseMeaning(binary.Right));
    }

    private static string? ClauseMeaning(ExpressionSyntax clause)
    {
        var name = ClauseNameDeriver.DeriveName(clause, clauseNumber: 0);
        return name.StartsWith("Clause", StringComparison.Ordinal) ? null : name;
    }

    private static string? JoinClauseMeanings(string? left, string connective, string? right)
    {
        if (left is null || right is null)
            return null;

        var leftWords = SplitWords(left);
        var rightWords = SplitWords(right);
        var shared = leftWords.Zip(rightWords, string.Equals).TakeWhile(same => same).Count();

        // "Is" alone is not a subject; only a shared "Is{Subject}" is stated once
        var rightRemainder = shared >= 2 && shared < rightWords.Count
            ? string.Concat(rightWords.Skip(shared))
            : right;

        var joined = $"{left}{connective}{rightRemainder}";
        return joined.Length <= MaxJoinedNameLength ? joined : null;
    }

    private static List<string> SplitWords(string pascalName) =>
        Regex.Split(pascalName, "(?<!^)(?=[A-Z])").ToList();

    private static string? GetConnective(BinaryExpressionSyntax binary) =>
        binary.OperatorToken.Kind() switch
        {
            SyntaxKind.AmpersandAmpersandToken => "And",
            SyntaxKind.BarBarToken => "Or",
            _ => null
        };

    private static bool ContainsLogicalOperator(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Any(binary =>
            binary.OperatorToken.Kind() is SyntaxKind.AmpersandAmpersandToken or SyntaxKind.BarBarToken or SyntaxKind.CaretToken);

    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;
        return expression;
    }

    private static string? EnclosingMemberName(ExpressionSyntax expression) =>
        expression.Ancestors()
            .Select(ancestor => ancestor switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                LocalFunctionStatementSyntax localFunction => localFunction.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                _ => null
            })
            .FirstOrDefault(name => name is not null && !IsGenericMethodName(name));

    /// <summary>
    /// Attempts to get the name of the variable to which the expression is assigned.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="name">The assignment target name if found.</param>
    /// <returns>True if an assignment target name was found; otherwise, false.</returns>
    private static bool TryGetAssignmentTargetName(ExpressionSyntax expression, out string name)
    {
        // Walk up the tree to find a VariableDeclarator
        var variableDeclarator = expression.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        if (variableDeclarator is not null)
        {
            name = variableDeclarator.Identifier.ValueText;
            return true;
        }

        name = string.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to get the name of the method from which the expression is returned.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="name">The method name if found and not generic.</param>
    /// <returns>True if a meaningful method name was found; otherwise, false.</returns>
    private static bool TryGetMethodReturnContextName(ExpressionSyntax expression, out string name)
    {
        // Walk up the tree to find a ReturnStatement, then find its containing method
        var returnStatement = expression.Ancestors()
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault();

        if (returnStatement is not null)
        {
            var method = returnStatement.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            var returnMethodName = method?.Identifier.ValueText;

            if (returnMethodName is not null && !IsGenericMethodName(returnMethodName))
            {
                name = returnMethodName;
                return true;
            }
        }

        // Check for expression-bodied method (=>)
        var arrowClause = expression.Ancestors()
            .OfType<ArrowExpressionClauseSyntax>()
            .FirstOrDefault();

        var arrowMemberName = arrowClause?.Parent switch
        {
            MethodDeclarationSyntax arrowMethod => arrowMethod.Identifier.ValueText,
            PropertyDeclarationSyntax arrowProperty => arrowProperty.Identifier.ValueText,
            LocalFunctionStatementSyntax arrowLocalFunction => arrowLocalFunction.Identifier.ValueText,
            _ => null
        };

        if (arrowMemberName is not null && !IsGenericMethodName(arrowMemberName))
        {
            name = arrowMemberName;
            return true;
        }

        name = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks if a method name is too generic to provide meaningful context.
    /// </summary>
    private static bool IsGenericMethodName(string methodName)
    {
        // Common generic method names that don't provide semantic meaning
        string[] genericNames =
        [
            "TestMethod",
            "Test",
            "Execute",
            "Run",
            "Process",
            "Handle",
            "Main"
        ];

        return genericNames.Contains(methodName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Derives the base name from the expression by finding the most common root identifier.
    /// </summary>
    private static string DeriveBaseName(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var rootIdentifiers = GetRootIdentifierNames(expression, semanticModel).ToList();

        if (rootIdentifiers.Count == 0)
        {
            return PropositionBaseName; // Fallback if no identifiers found
        }

        // Find most common identifier
        var groups = rootIdentifiers
            .GroupBy(name => name)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key) // Deterministic tie-break (alphabetical)
            .ToList();

        var mostCommon = groups.First();

        // If all identifiers are distinct AND there are multiple (no common root), use fallback
        if (rootIdentifiers.Distinct().Count() == rootIdentifiers.Count && rootIdentifiers.Count > 1)
        {
            return PropositionBaseName;
        }

        return mostCommon.Key;
    }

    /// <summary>
    /// Extracts root identifier names from the expression.
    /// Filters to only include variable identifiers (not member access suffixes, not types).
    /// </summary>
    private static IEnumerable<string> GetRootIdentifierNames(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        // Get all identifiers from the expression (including inside is-pattern expressions)
        var identifiers = GetIdentifiersFromExpression(expression);

        return identifiers
            .Where(IsRootIdentifier)
            .Select(id => id.Identifier.ValueText)
            .Where(name => IsVariableIdentifier(name, expression, semanticModel));
    }

    /// <summary>
    /// Gets all identifiers from the expression, excluding those in pattern types.
    /// For is-pattern expressions, only extracts from the tested expression, not the pattern.
    /// </summary>
    private static IEnumerable<IdentifierNameSyntax> GetIdentifiersFromExpression(ExpressionSyntax expression)
    {
        // Special handling for is-pattern expressions
        if (expression is IsPatternExpressionSyntax isPattern)
        {
            // Only get identifiers from the expression being tested (left side of 'is')
            // NOT from the pattern (right side of 'is', which contains the type)
            return isPattern.Expression
                .DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>();
        }

        // For all other expressions, get all identifiers
        return expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>();
    }

    /// <summary>
    /// Checks if an identifier is a root identifier (not the right side of member access).
    /// For example, in "order.Total", "order" is root but "Total" is not.
    /// </summary>
    private static bool IsRootIdentifier(IdentifierNameSyntax identifier)
    {
        // If parent is MemberAccessExpression and identifier is on the right side (Name), exclude it
        if (identifier.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == identifier)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if an identifier name represents a variable (parameter, local, field, or property).
    /// </summary>
    private static bool IsVariableIdentifier(
        string name,
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        // Find the identifier in the expression
        var identifierNode = expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .FirstOrDefault(id => id.Identifier.ValueText == name);

        if (identifierNode is null)
        {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(identifierNode).Symbol;
        return symbol is IFieldSymbol or IPropertySymbol or ILocalSymbol or IParameterSymbol;
    }

    /// <summary>
    /// Ensures the name is unique by checking for collisions and appending incrementing numbers if needed.
    /// </summary>
    private static string EnsureUniqueName(string baseName, SemanticModel semanticModel, int position)
    {
        var candidateName = baseName;
        var counter = 0;

        while (TypeNameExists(candidateName, semanticModel, position))
        {
            counter++;
            candidateName = $"{baseName}{counter}";
        }

        return candidateName;
    }

    /// <summary>
    /// Checks if a type name already exists at the given position.
    /// </summary>
    private static bool TypeNameExists(string name, SemanticModel semanticModel, int position)
    {
        var symbols = semanticModel.LookupSymbols(position, name: name);
        return symbols.Any(s => s.Kind == SymbolKind.NamedType);
    }
}
