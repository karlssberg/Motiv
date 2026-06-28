using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Derives meaningful names for clause variables from boolean expressions.
/// </summary>
public static class ClauseNameDeriver
{
    private const int MaxNameLength = 50;

    private static readonly HashSet<string> CSharpKeywordTypes = new(StringComparer.Ordinal)
    {
        "string", "int", "long", "short", "byte", "sbyte",
        "uint", "ulong", "ushort", "float", "double", "decimal",
        "bool", "char", "object", "nint", "nuint"
    };

    /// <summary>
    /// Derives a semantic name for a clause from its expression.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="clauseNumber">The clause number to use as fallback.</param>
    /// <returns>A derived name or "Clause{N}" as fallback.</returns>
    public static string DeriveName(ExpressionSyntax expression, int clauseNumber)
    {
        if (!TryExtractNamePart(expression, out var part) || part.Length == 0)
            return $"Clause{clauseNumber}";

        var name = part.StartsWith("Is", StringComparison.Ordinal) ||
                   part.StartsWith("Has", StringComparison.Ordinal) ||
                   part.StartsWith("DoesNotHave", StringComparison.Ordinal)
            ? part
            : $"Is{part}";

        return name.Length <= MaxNameLength
            ? name
            : $"Clause{clauseNumber}";
    }

    /// <summary>
    /// Recursively extracts a name fragment from any expression.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <param name="part">The extracted name fragment.</param>
    /// <returns>True if a name part was extracted; otherwise, false.</returns>
    private static bool TryExtractNamePart(ExpressionSyntax expression, out string part)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax paren:
                    expression = paren.Expression;
                    continue;

                case IdentifierNameSyntax identifier:
                    return TryExtractIdentifierPart(identifier, out part);

                case MemberAccessExpressionSyntax memberAccess:
                    return TryExtractMemberAccessPart(memberAccess, out part);

                case LiteralExpressionSyntax literal:
                    return TryExtractLiteralPart(literal, out part);

                case BinaryExpressionSyntax binary:
                    return TryExtractBinaryPart(binary, out part);

                case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken } unary:
                    return TryExtractNegationPart(unary, out part);

                case IsPatternExpressionSyntax isPattern:
                    return TryExtractIsPatternPart(isPattern, out part);

                case InvocationExpressionSyntax invocation:
                    return TryExtractInvocationPart(invocation, out part);

                case ConditionalAccessExpressionSyntax conditionalAccess:
                    return TryExtractConditionalAccessPart(conditionalAccess, out part);

                default:
                    part = string.Empty;
                    return false;
            }
        }
    }

    /// <summary>
    /// Extracts a name part from an identifier, filtering lambda parameter names.
    /// </summary>
    private static bool TryExtractIdentifierPart(IdentifierNameSyntax identifier, out string part)
    {
        var name = identifier.Identifier.ValueText;

        if (IsLambdaParameterName(name))
        {
            part = string.Empty;
            return false;
        }

        part = name.Capitalize();
        return true;
    }

    /// <summary>
    /// Extracts a name part from a member access chain, filtering lambda parameter prefixes.
    /// </summary>
    private static bool TryExtractMemberAccessPart(MemberAccessExpressionSyntax memberAccess, out string part)
    {
        part = ExtractIdentifierChain(memberAccess) ?? string.Empty;
        return part.Length > 0;
    }

    /// <summary>
    /// Extracts a name part from a literal expression.
    /// </summary>
    private static bool TryExtractLiteralPart(LiteralExpressionSyntax literal, out string part)
    {
        var value = literal.Token.ValueText;

        if (literal.IsKind(SyntaxKind.StringLiteralExpression))
            value = CleanForIdentifier(value);

        if (value.Length == 0)
        {
            part = string.Empty;
            return false;
        }

        part = value.Capitalize();
        return true;
    }

    /// <summary>
    /// Extracts a name part from a binary expression, composing left and right parts.
    /// </summary>
    private static bool TryExtractBinaryPart(BinaryExpressionSyntax binary, out string part)
    {
        // Handle coalesce operator (??) — use only the left side
        if (binary.IsKind(SyntaxKind.CoalesceExpression))
        {
            return TryExtractNamePart(binary.Left, out part);
        }

        var operatorVerb = GetOperatorVerb(binary.OperatorToken.Kind());
        if (operatorVerb is null
            || !TryExtractNamePart(binary.Left, out var leftPart)
            || !TryExtractNamePart(binary.Right, out var rightPart))
        {
            part = string.Empty;
            return false;
        }

        // Special cases for zero comparisons
        if (rightPart == "0")
        {
            var zeroName = GenerateZeroComparisonName(binary, leftPart);
            if (zeroName is not null)
            {
                part = zeroName;
                return true;
            }
        }

        if (leftPart == "0")
        {
            var zeroName = GenerateReversedZeroComparisonName(binary, rightPart);
            if (zeroName is not null)
            {
                part = zeroName;
                return true;
            }
        }

        // Special cases for null comparisons
        if (rightPart.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            var nullName = GenerateNullComparisonName(binary, leftPart);
            if (nullName is not null)
            {
                part = nullName;
                return true;
            }
        }

        if (leftPart.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            var nullName = GenerateNullComparisonName(binary, rightPart);
            if (nullName is not null)
            {
                part = nullName;
                return true;
            }
        }

        // General pattern: {Left}{Verb}{Right}
        part = $"{leftPart}{operatorVerb}{rightPart}";
        return true;
    }

    /// <summary>
    /// Extracts a name part from a negation expression.
    /// </summary>
    private static bool TryExtractNegationPart(PrefixUnaryExpressionSyntax unary, out string part)
    {
        if (!TryExtractNamePart(unary.Operand, out var operandPart))
        {
            part = string.Empty;
            return false;
        }

        part = NegateNamePart(operandPart);
        return true;
    }

    /// <summary>
    /// Negates a name part by toggling known prefixes (Is/IsNot, Has/DoesNotHave, Not).
    /// </summary>
    private static string NegateNamePart(string operandPart)
    {
        // Handle double negation: "IsNot{X}" → "Is{X}"
        if (HasPrefixFollowedByUpperCase(operandPart, "IsNot"))
            return $"Is{operandPart.Substring(5)}";

        // Handle "IsXxx" → "IsNotXxx"
        if (HasPrefixFollowedByUpperCase(operandPart, "Is"))
            return $"IsNot{operandPart.Substring(2)}";

        // Handle double negation: "DoesNotHave{X}" → "Has{X}"
        if (HasPrefixFollowedByUpperCase(operandPart, "DoesNotHave"))
            return $"Has{operandPart.Substring(11)}";

        // Handle "HasXxx" → "DoesNotHaveXxx"
        if (HasPrefixFollowedByUpperCase(operandPart, "Has"))
            return $"DoesNotHave{operandPart.Substring(3)}";

        // Handle double negation: "Not{X}" → just return X
        if (HasPrefixFollowedByUpperCase(operandPart, "Not"))
            return operandPart.Substring(3);

        // Default negation
        return $"Not{operandPart}";
    }

    /// <summary>
    /// Checks if a name starts with the given prefix followed by an uppercase letter.
    /// </summary>
    private static bool HasPrefixFollowedByUpperCase(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal) &&
        name.Length > prefix.Length &&
        char.IsUpper(name[prefix.Length]);

    /// <summary>
    /// Extracts a name part from an is-pattern expression.
    /// </summary>
    private static bool TryExtractIsPatternPart(IsPatternExpressionSyntax isPattern, out string part)
    {
        if (!TryExtractNamePart(isPattern.Expression, out var exprPart))
        {
            part = string.Empty;
            return false;
        }

        if (TryExtractPatternPart(isPattern.Pattern, out var patternPart))
        {
            part = $"{exprPart}{patternPart}";
            return true;
        }

        part = string.Empty;
        return false;
    }

    /// <summary>
    /// Extracts a name part from a pattern syntax node.
    /// </summary>
    private static bool TryExtractPatternPart(PatternSyntax pattern, out string part)
    {
        switch (pattern)
        {
            case TypePatternSyntax typePattern:
                part = typePattern.Type.ToString().Replace(".", "").Capitalize();
                return true;

            case ConstantPatternSyntax constant:
                if (constant.Expression is LiteralExpressionSyntax literal)
                {
                    part = literal.Token.ValueText.Capitalize();
                    return true;
                }
                part = string.Empty;
                return false;

            case UnaryPatternSyntax { OperatorToken.RawKind: (int)SyntaxKind.NotKeyword } unaryPattern:
                if (TryExtractPatternPart(unaryPattern.Pattern, out var innerPart))
                {
                    part = $"Not{innerPart}";
                    return true;
                }
                part = string.Empty;
                return false;

            default:
                part = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Extracts a name part from an invocation expression.
    /// </summary>
    private static bool TryExtractInvocationPart(InvocationExpressionSyntax invocation, out string part)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
            {
                var methodName = memberAccess.Name.Identifier.ValueText.Capitalize();

                // For static calls on C# keyword types (e.g., string.IsNullOrEmpty), skip the receiver
                if (memberAccess.Expression is IdentifierNameSyntax receiverIdentifier &&
                    CSharpKeywordTypes.Contains(receiverIdentifier.Identifier.ValueText))
                {
                    part = methodName;
                    return true;
                }

                // For instance calls, compose receiver + method name
                if (TryExtractNamePart(memberAccess.Expression, out var receiverPart))
                {
                    part = $"{receiverPart}{methodName}";
                    return true;
                }

                part = methodName;
                return true;
            }

            case IdentifierNameSyntax identifier:
            {
                part = identifier.Identifier.ValueText.Capitalize();
                return true;
            }

            default:
                part = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Extracts a name part from a conditional access expression (e.g., item?.IsValid).
    /// </summary>
    private static bool TryExtractConditionalAccessPart(ConditionalAccessExpressionSyntax conditionalAccess,
        out string part)
    {
        if (!TryExtractNamePart(conditionalAccess.Expression, out var receiverPart))
        {
            part = string.Empty;
            return false;
        }

        var binding = conditionalAccess.WhenNotNull switch
        {
            MemberBindingExpressionSyntax member => member,
            InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax method } => method,
            _ => null
        };

        if (binding is not null)
        {
            var memberName = binding.Name.Identifier.ValueText.Capitalize();
            part = $"{receiverPart}{memberName}";
            return true;
        }

        part = receiverPart;
        return true;
    }

    /// <summary>
    /// Extracts a concatenated identifier chain from a member access expression.
    /// Filters out lambda parameter names like "m" and "model".
    /// </summary>
    private static string? ExtractIdentifierChain(ExpressionSyntax expression)
    {
        var parts = new List<string>();
        ExtractIdentifiersRecursive(expression, parts);

        if (parts.Count == 0)
            return null;

        var result = string.Join("", parts
            .Where(p => !IsLambdaParameterName(p))
            .Select(p => p.Capitalize()));

        return result.Length > 0 ? result : null;
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
    /// Checks if a name is a common lambda parameter name that should be filtered out.
    /// </summary>
    private static bool IsLambdaParameterName(string name) =>
        string.Equals(name, "m", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "model", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "instance", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strips non-alphanumeric characters from a string for use in identifiers.
    /// </summary>
    private static string CleanForIdentifier(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray());

    /// <summary>
    /// Maps operator tokens to verb forms for naming.
    /// </summary>
    private static string? GetOperatorVerb(SyntaxKind operatorKind) =>
        operatorKind switch
        {
            SyntaxKind.GreaterThanToken => "GreaterThan",
            SyntaxKind.LessThanToken => "LessThan",
            SyntaxKind.GreaterThanEqualsToken => "AtLeast",
            SyntaxKind.LessThanEqualsToken => "AtMost",
            SyntaxKind.EqualsEqualsToken => "",
            SyntaxKind.ExclamationEqualsToken => "Not",
            _ => null
        };

    /// <summary>
    /// Generates a name for a zero comparison where the variable is on the left (e.g., x > 0).
    /// Returns raw name part without "Is" prefix.
    /// </summary>
    private static string? GenerateZeroComparisonName(BinaryExpressionSyntax binary, string variableName) =>
        binary.OperatorToken.Kind() switch
        {
            SyntaxKind.GreaterThanEqualsToken => $"{variableName}NonNegative",
            SyntaxKind.LessThanToken => $"{variableName}Negative",
            SyntaxKind.EqualsEqualsToken => $"{variableName}Zero",
            SyntaxKind.ExclamationEqualsToken => $"{variableName}NotZero",
            SyntaxKind.GreaterThanToken => $"{variableName}Positive",
            SyntaxKind.LessThanEqualsToken => $"{variableName}NonPositive",
            _ => null
        };

    /// <summary>
    /// Generates a name for a zero comparison where the variable is on the right (e.g., 0 &lt; x).
    /// Returns raw name part without "Is" prefix.
    /// </summary>
    private static string? GenerateReversedZeroComparisonName(BinaryExpressionSyntax binary, string variableName) =>
        binary.OperatorToken.Kind() switch
        {
            SyntaxKind.LessThanEqualsToken => $"{variableName}NonNegative",
            SyntaxKind.GreaterThanToken => $"{variableName}Negative",
            SyntaxKind.EqualsEqualsToken => $"{variableName}Zero",
            SyntaxKind.ExclamationEqualsToken => $"{variableName}NotZero",
            SyntaxKind.LessThanToken => $"{variableName}Positive",
            SyntaxKind.GreaterThanEqualsToken => $"{variableName}NonPositive",
            _ => null
        };

    /// <summary>
    /// Generates a name for a null comparison (e.g., x == null or null == x).
    /// Returns raw name part without "Is" prefix.
    /// </summary>
    private static string? GenerateNullComparisonName(BinaryExpressionSyntax binary, string variableName) =>
        binary.OperatorToken.Kind() switch
        {
            SyntaxKind.EqualsEqualsToken => $"{variableName}Null",
            SyntaxKind.ExclamationEqualsToken => $"{variableName}NotNull",
            _ => null
        };
}
