using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Derives meaningful names for clause variables from boolean expressions.
/// </summary>
public static class ClauseNameDeriver
{
    private const int MaxNameLength = 50;

    /// <summary>
    /// Derives a semantic name for a clause from its expression.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="clauseNumber">The clause number to use as fallback.</param>
    /// <returns>A derived name or "Clause{N}" as fallback.</returns>
    public static string DeriveName(ExpressionSyntax expression, int clauseNumber)
    {
        // Try to derive a semantic name
        return TryDeriveName(expression, out var derivedName) && derivedName.Length <= MaxNameLength
            ? derivedName
            : $"Clause{clauseNumber}";
    }

    /// <summary>
    /// Attempts to derive a semantic name from the expression.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived with confidence; otherwise, false.</returns>
    private static bool TryDeriveName(ExpressionSyntax expression, out string name)
    {
        switch (expression)
        {
            // Handle binary expressions (comparisons)
            case BinaryExpressionSyntax binary:
                return TryDeriveBinaryName(binary, out name);

            // Handle unary expressions (negation)
            case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } unary:
                return TryDeriveNegationName(unary, out name);

            // Handle is-pattern expressions
            case IsPatternExpressionSyntax isPattern:
                return TryDeriveIsPatternName(isPattern, out name);

            // Handle simple identifier (boolean property)
            case IdentifierNameSyntax identifier:
                name = TryDeriveIdentifierName(identifier);
                return true;

            // Handle member access (e.g., obj.Property)
            case MemberAccessExpressionSyntax memberAccess:
                return TryDeriveMemberAccessName(memberAccess, out name);

            // Handle parenthesized expressions - unwrap and try again
            case ParenthesizedExpressionSyntax paren:
                return TryDeriveName(paren.Expression, out name);

            // Complex expression - return false to trigger fallback
            default:
                name = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Tries to derive a name from a binary expression (e.g., comparisons).
    /// </summary>
    /// <param name="binary">The binary expression to analyze.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived; otherwise, false.</returns>
    private static bool TryDeriveBinaryName(BinaryExpressionSyntax binary, out string name)
    {
        // Try normal pattern: variable on left, literal on right
        var leftName = ExtractIdentifierChain(binary.Left);
        var rightValue = ExtractLiteralValue(binary.Right);

        if (leftName is not null)
        {
            return TryDeriveNameWithLeftVariable(binary, leftName, rightValue, out name);
        }

        // Try reversed pattern: literal on left, variable on right
        var leftValue = ExtractLiteralValue(binary.Left);
        var rightName = ExtractIdentifierChain(binary.Right);

        if (leftValue is not null && rightName is not null)
        {
            return TryDeriveNameWithRightVariable(binary, leftValue, rightName, out name);
        }

        // If neither pattern matches, fall back
        name = string.Empty;
        return false;
    }

    /// <summary>
    /// Derives a name when the variable is on the left side of the comparison.
    /// </summary>
    /// <param name="binary">The binary expression.</param>
    /// <param name="leftName">The left variable name.</param>
    /// <param name="rightValue">The right literal value.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived; otherwise, false.</returns>
    private static bool TryDeriveNameWithLeftVariable(
        BinaryExpressionSyntax binary,
        string leftName,
        string? rightValue,
        out string name)
    {
        // Get the operator verb
        var operatorVerb = GetOperatorVerb(binary.OperatorToken.Kind());
        if (operatorVerb is null)
        {
            name = string.Empty;
            return false;
        }

        // Special cases for common patterns
        if (rightValue == "0")
        {
            var zeroName = GenerateZeroComparisonName();

            if (zeroName is not null)
            {
                name = zeroName;
                return true;
            }
        }

        if (rightValue == "null")
        {
            var nullName = GenerateIsNullOrNotNullName();

            if (nullName is not null)
            {
                name = nullName;
                return true;
            }
        }

        // General pattern: Is{Property}{Operator}{Value}
        if (rightValue is not null)
        {
            name = $"Is{leftName}{operatorVerb}{rightValue}";
            return true;
        }

        // If right side isn't a simple literal, fall back
        name = string.Empty;
        return false;

        string? GenerateZeroComparisonName()
        {
            var zeroName = binary.OperatorToken.Kind() switch
            {
                SyntaxKind.GreaterThanEqualsToken => $"Is{leftName}NonNegative",
                SyntaxKind.LessThanToken => $"Is{leftName}Negative",
                SyntaxKind.EqualsEqualsToken => $"Is{leftName}Zero",
                SyntaxKind.ExclamationEqualsToken => $"Is{leftName}NotZero",
                SyntaxKind.GreaterThanToken => $"Is{leftName}Positive",
                SyntaxKind.LessThanEqualsToken => $"Is{leftName}NonPositive",
                _ => null
            };
            return zeroName;
        }

        string? GenerateIsNullOrNotNullName()
        {
            var nullName = binary.OperatorToken.Kind() switch
            {
                SyntaxKind.EqualsEqualsToken => $"Is{leftName}Null",
                SyntaxKind.ExclamationEqualsToken => $"Is{leftName}NotNull",
                _ => null
            };
            return nullName;
        }
    }

    /// <summary>
    /// Derives a name when the variable is on the right side of the comparison.
    /// Pattern: Is{Literal}{Operator}{Variable}
    /// </summary>
    /// <param name="binary">The binary expression.</param>
    /// <param name="leftValue">The left literal value.</param>
    /// <param name="rightName">The right variable name.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived; otherwise, false.</returns>
    private static bool TryDeriveNameWithRightVariable(
        BinaryExpressionSyntax binary,
        string leftValue,
        string rightName,
        out string name)
    {
        // Get the operator verb (same as in normal case)
        var operatorVerb = GetOperatorVerb(binary.OperatorToken.Kind());
        if (operatorVerb is null)
        {
            name = string.Empty;
            return false;
        }

        switch (leftValue)
        {
            // Special cases for zero comparisons with reversed operands
            case "0":
            {
                var zeroName = GenerateZeroComparisonName();

                if (zeroName is not null)
                {
                    name = zeroName;
                    return true;
                }

                break;
            }
            case "null":
            {
                var nullName = GenerateIsNullOrNotNullName();

                if (nullName is not null)
                {
                    name = nullName;
                    return true;
                }

                break;
            }
        }

        // General pattern: Is{Value}{Operator}{Property}
        // Example: "1 < valueC" becomes "Is1LessThanValueC"
        name = $"Is{leftValue}{operatorVerb}{rightName}";
        return true;

        string? GenerateZeroComparisonName()
        {
            var zeroName = binary.OperatorToken.Kind() switch
            {
                SyntaxKind.LessThanEqualsToken => $"Is{rightName}NonNegative",  // 0 <= x means x >= 0
                SyntaxKind.GreaterThanToken => $"Is{rightName}Negative",         // 0 > x means x < 0
                SyntaxKind.EqualsEqualsToken => $"Is{rightName}Zero",            // 0 == x means x == 0
                SyntaxKind.ExclamationEqualsToken => $"Is{rightName}NotZero",    // 0 != x means x != 0
                SyntaxKind.LessThanToken => $"Is{rightName}Positive",            // 0 < x means x > 0
                SyntaxKind.GreaterThanEqualsToken => $"Is{rightName}NonPositive", // 0 >= x means x <= 0
                _ => null
            };
            return zeroName;
        }

        string? GenerateIsNullOrNotNullName()
        {
            var nullName = binary.OperatorToken.Kind() switch
            {
                SyntaxKind.EqualsEqualsToken => $"Is{rightName}Null",
                SyntaxKind.ExclamationEqualsToken => $"Is{rightName}NotNull",
                _ => null
            };
            return nullName;
        }
    }

    /// <summary>
    /// Tries to derive a name from a negation expression.
    /// </summary>
    /// <param name="unary">The unary expression.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived; otherwise, false.</returns>
    private static bool TryDeriveNegationName(PrefixUnaryExpressionSyntax unary, out string name)
    {
        var operandName = ExtractIdentifierChain(unary.Operand);
        if (operandName is null)
        {
            name = string.Empty;
            return false;
        }

        // Handle "IsXxx" → "IsNotXxx"
        if (operandName.StartsWith("Is", StringComparison.Ordinal))
        {
            name = $"IsNot{operandName.Substring(2)}";
            return true;
        }

        // Handle "HasXxx" → "DoesNotHaveXxx"
        if (operandName.StartsWith("Has", StringComparison.Ordinal))
        {
            name = $"DoesNotHave{operandName.Substring(3)}";
            return true;
        }

        // Default negation
        name = $"IsNot{operandName}";
        return true;
    }

    /// <summary>
    /// Tries to derive a name from an is-pattern expression.
    /// </summary>
    /// <param name="isPattern">The is-pattern expression.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived; otherwise, false.</returns>
    private static bool TryDeriveIsPatternName(IsPatternExpressionSyntax isPattern, out string name)
    {
        var expressionName = ExtractIdentifierChain(isPattern.Expression);
        if (expressionName is null)
        {
            name = string.Empty;
            return false;
        }

        // Extract type name from pattern
        if (isPattern.Pattern is TypePatternSyntax typePattern)
        {
            var typeName = typePattern.Type.ToString().Replace(".", "");
            name = $"Is{expressionName}{typeName}";
            return true;
        }

        name = string.Empty;
        return false;
    }

    /// <summary>
    /// Tries to derive a name from a simple identifier (boolean property).
    /// </summary>
    private static string TryDeriveIdentifierName(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.ValueText;

        // Already has "Is" prefix
        if (name.StartsWith("is", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("has", StringComparison.OrdinalIgnoreCase))
        {
            return name.Capitalize();
        }

        // Add "Is" prefix
        return $"Is{name.Capitalize()}";
    }

    /// <summary>
    /// Tries to derive a name from member access (e.g., obj.Property).
    /// </summary>
    /// <param name="memberAccess">The member access expression.</param>
    /// <param name="name">The derived name if successful.</param>
    /// <returns>True if a name was derived; otherwise, false.</returns>
    private static bool TryDeriveMemberAccessName(MemberAccessExpressionSyntax memberAccess, out string name)
    {
        var chain = ExtractIdentifierChain(memberAccess);
        if (chain is not null)
        {
            name = $"Is{chain}";
            return true;
        }

        name = string.Empty;
        return false;
    }

    /// <summary>
    /// Extracts a concatenated identifier chain from an expression.
    /// E.g., "m.Order.Total" → "OrderTotal", "age" → "Age"
    /// </summary>
    private static string? ExtractIdentifierChain(ExpressionSyntax expression)
    {
        var parts = new List<string>();

        ExtractIdentifiersRecursive(expression, parts);

        if (parts.Count == 0)
        {
            return null;
        }

        // Skip "m" or "model" prefixes (common in lambda parameters)
        var filtered = parts
            .Where(p => !string.Equals(p, "m", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(p, "model", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return filtered.Count > 0
            ? string.Join("", filtered.Select(p => p.Capitalize()))
            : null;
    }

    /// <summary>
    /// Recursively extracts identifiers from member access chains.
    /// </summary>
    private static void ExtractIdentifiersRecursive(ExpressionSyntax expression, List<string> parts)
    {
        switch (expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                ExtractIdentifiersRecursive(memberAccess.Expression, parts);
                parts.Add(memberAccess.Name.Identifier.ValueText);
                break;

            case IdentifierNameSyntax identifier:
                parts.Add(identifier.Identifier.ValueText);
                break;
        }
    }

    /// <summary>
    /// Extracts a literal value as a string (for use in names).
    /// </summary>
    private static string? ExtractLiteralValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            _ => null
        };
    }

    /// <summary>
    /// Maps operator tokens to verb forms for naming.
    /// </summary>
    private static string? GetOperatorVerb(SyntaxKind operatorKind)
    {
        return operatorKind switch
        {
            SyntaxKind.GreaterThanToken => "GreaterThan",
            SyntaxKind.LessThanToken => "LessThan",
            SyntaxKind.GreaterThanEqualsToken => "AtLeast",
            SyntaxKind.LessThanEqualsToken => "AtMost",
            SyntaxKind.EqualsEqualsToken => "",  // For "Is{Property}5" instead of "Is{Property}Equals5"
            SyntaxKind.ExclamationEqualsToken => "Not",
            _ => null
        };
    }
}
