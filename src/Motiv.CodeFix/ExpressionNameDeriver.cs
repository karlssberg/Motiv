using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Derives meaningful class names from boolean expression content.
/// </summary>
public static class ExpressionNameDeriver
{
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

        // Step 2: Convert to PascalCase
        var pascalName = baseName.Capitalize();

        // Step 3: Append suffixes (unless base name is already the fallback)
        var propositionName = pascalName == "Proposition" ? "Proposition" : $"{pascalName}Proposition";
        var modelName = pascalName == "Proposition" ? "Model" : $"{pascalName}Model";

        // Step 4: Ensure uniqueness
        propositionName = EnsureUniqueName(propositionName, semanticModel, insertionPosition);
        modelName = EnsureUniqueName(modelName, semanticModel, insertionPosition);

        return (propositionName, modelName);
    }

    /// <summary>
    /// Derives the base name with context awareness, following precedence order.
    /// </summary>
    private static string DeriveBaseNameFromContext(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // Priority 1: Check if expression is assigned to a variable
        var assignmentName = TryGetAssignmentTargetName(expression);
        if (assignmentName != null)
        {
            return assignmentName;
        }

        // Priority 2: Check if expression is returned from a method
        var methodName = TryGetMethodReturnContextName(expression);
        if (methodName != null)
        {
            return methodName;
        }

        // Priority 3: Derive from expression content
        return DeriveBaseName(expression, semanticModel);
    }

    /// <summary>
    /// Attempts to get the name of the variable to which the expression is assigned.
    /// </summary>
    private static string? TryGetAssignmentTargetName(ExpressionSyntax expression)
    {
        // Walk up the tree to find a VariableDeclarator
        var variableDeclarator = expression.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        return variableDeclarator?.Identifier.ValueText;
    }

    /// <summary>
    /// Attempts to get the name of the method from which the expression is returned.
    /// </summary>
    private static string? TryGetMethodReturnContextName(ExpressionSyntax expression)
    {
        // Walk up the tree to find a ReturnStatement, then find its containing method
        var returnStatement = expression.Ancestors()
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault();

        if (returnStatement == null)
        {
            return null;
        }

        var methodDeclaration = returnStatement.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        var methodName = methodDeclaration?.Identifier.ValueText;

        // Filter out generic test method names that don't provide meaningful context
        if (methodName == null || IsGenericMethodName(methodName))
        {
            return null;
        }

        return methodName;
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
            return "Proposition"; // Fallback if no identifiers found
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
            return "Proposition";
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

        if (identifierNode == null)
        {
            return false;
        }

        var symbol = ModelExtensions.GetSymbolInfo(semanticModel, identifierNode).Symbol;
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
