using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Syntax;

/// <summary>
/// Provides factory methods for creating custom specification declarations.
/// </summary>
public static class CustomSpecDeclarationSyntax
{
    /// <summary>
    /// Parameters for creating a composed specification with constructor.
    /// </summary>
    private readonly struct ConstructorSpecParams
    {
        public ConstructorSpecParams(
            string propositionName,
            string modelName,
            string? singleModelTypeName,
            string? recordParameters,
            IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
            string compositionExpression,
            string containingTypeName)
        {
            PropositionName = propositionName;
            ModelName = modelName;
            SingleModelTypeName = singleModelTypeName;
            RecordParameters = recordParameters;
            Clauses = clauses;
            CompositionExpression = compositionExpression;
            ContainingTypeName = containingTypeName;
        }

        public string PropositionName { get; }
        public string ModelName { get; }
        public string? SingleModelTypeName { get; }
        public string? RecordParameters { get; }
        public IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> Clauses { get; }
        public string CompositionExpression { get; }
        public string ContainingTypeName { get; }
    }

    /// <summary>
    /// Parameters for creating a composed specification.
    /// </summary>
    private readonly struct ComposedSpecParams
    {
        public ComposedSpecParams(
            string propositionName,
            string modelName,
            string recordParameters,
            IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
            string compositionExpression)
        {
            PropositionName = propositionName;
            ModelName = modelName;
            RecordParameters = recordParameters;
            Clauses = clauses;
            CompositionExpression = compositionExpression;
        }

        public string PropositionName { get; }
        public string ModelName { get; }
        public string RecordParameters { get; }
        public IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> Clauses { get; }
        public string CompositionExpression { get; }
    }

    /// <summary>
    /// Result of clause deduplication containing unique clauses and name mappings.
    /// </summary>
    private readonly struct DeduplicatedClauses
    {
        public DeduplicatedClauses(
            Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string DerivedName)> uniqueClauses,
            Dictionary<int, string> clauseNameMapping)
        {
            UniqueClauses = uniqueClauses;
            ClauseNameMapping = clauseNameMapping;
        }

        public Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string DerivedName)> UniqueClauses { get; }
        public Dictionary<int, string> ClauseNameMapping { get; }
    }

    /// <summary>
    /// Creates a type declaration for a custom specification.
    /// </summary>
    /// <param name="propositionName">The name of the proposition.</param>
    /// <param name="modelParameterName">The name of the model parameter.</param>
    /// <param name="logicalExpression">The logical expression.</param>
    /// <param name="modelTypeName">The name of the model type.</param>
    /// <returns>A type declaration syntax node.</returns>
    public static TypeDeclarationSyntax Create(
        IdentifierNameSyntax propositionName,
        IdentifierNameSyntax modelParameterName,
        ExpressionSyntax logicalExpression,
        string modelTypeName)
    {
        return CreateInternal(
            propositionName.ToString(),
            modelParameterName.ToString(),
            logicalExpression,
            logicalExpression,
            modelTypeName);
    }

    /// <summary>
    /// Creates a constructor-based specification for expressions with instance method calls.
    /// </summary>
    public static TypeDeclarationSyntax CreateWithConstructor(
        string propositionName,
        string modelName,
        string? singleModelTypeName,
        string? recordParameters,
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        string compositionExpression,
        string containingTypeName)
    {
        var param = new ConstructorSpecParams(
            propositionName,
            modelName,
            singleModelTypeName,
            recordParameters,
            clauses,
            compositionExpression,
            containingTypeName);

        return CreateWithConstructorInternal(param);
    }

    private static TypeDeclarationSyntax CreateWithConstructorInternal(ConstructorSpecParams param)
    {
        // Determine the model type
        var modelType = param.SingleModelTypeName ?? param.ModelName;
        var fullModelType = param.SingleModelTypeName ?? $"{param.PropositionName}.{param.ModelName}";
        var hasRecordModel = param.SingleModelTypeName is null;

        // Deduplicate clauses
        var deduplicated = DeduplicateClauses(param.Clauses);

        // Update composition expression with camelCase names
        var updatedComposition = UpdateCompositionWithCamelCaseNames(
            param.CompositionExpression,
            param.Clauses,
            deduplicated.ClauseNameMapping);

        var sb = new System.Text.StringBuilder();

        // Generate class declaration with primary constructor and factory lambda
        sb.AppendLine($"public class {param.PropositionName}({param.ContainingTypeName} instance) : Spec<{fullModelType}>(() =>");
        sb.AppendLine("{");

        // Generate local variables for each clause inside factory lambda
        foreach (var (original, transformed, expression, derivedName) in deduplicated.UniqueClauses.Values)
        {
            var camelCaseName = ToCamelCase(derivedName);

            // Check if this expression contains instance method calls
            var hasInstanceMethod = expression.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.Expression is IdentifierNameSyntax);

            // Determine lambda body - inject instance reference for instance method calls
            string lambdaBody;
            if (hasRecordModel)
            {
                // For record model, use transformed text but also replace instance methods
                lambdaBody = hasInstanceMethod
                    ? ReplaceInstanceMethodCalls(transformed)
                    : transformed;
            }
            else
            {
                lambdaBody = hasInstanceMethod
                    ? ReplaceInstanceMethodCalls(expression.ToString())
                    : expression.ToString();
            }

            var paramName = hasRecordModel ? "m" : GetParameterNameFromExpression(expression);

            sb.AppendLine($"    var {camelCaseName} = Spec.Build(({modelType} {paramName}) => {lambdaBody})");
            sb.AppendLine($"        .WhenTrue(\"{original.EscapeDoubleQuotes()} == true\")");
            sb.AppendLine($"        .WhenFalse(\"{original.EscapeDoubleQuotes()} == false\")");
            sb.AppendLine($"        .Create();");
            sb.AppendLine();
        }

        // Return composition expression
        sb.AppendLine($"    return {updatedComposition};");

        // Add record if needed (inside class body)
        if (hasRecordModel)
        {
            sb.AppendLine("})");
            sb.AppendLine("{");
            sb.AppendLine($"    public record {param.ModelName}({param.RecordParameters});");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("});");
        }

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(sb.ToString());
        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    /// <summary>
    /// Deduplicates clauses based on their transformed text, assigning derived names.
    /// </summary>
    private static DeduplicatedClauses DeduplicateClauses(
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses)
    {
        var uniqueClauses = new Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string DerivedName)>();
        var clauseNameMapping = new Dictionary<int, string>();

        for (var i = 0; i < clauses.Count; i++)
        {
            var (original, transformed, expression) = clauses[i];

            if (!uniqueClauses.ContainsKey(transformed))
            {
                var derivedName = ClauseNameDeriver.DeriveName(expression, uniqueClauses.Count + 1);
                uniqueClauses[transformed] = (original, transformed, expression, derivedName);
                clauseNameMapping[i] = derivedName;
            }
            else
            {
                clauseNameMapping[i] = uniqueClauses[transformed].DerivedName;
            }
        }

        return new DeduplicatedClauses(uniqueClauses, clauseNameMapping);
    }

    private static string GetParameterNameFromExpression(ExpressionSyntax expression)
    {
        // Look for identifiers that are actual parameters/variables, not method or type names
        // For "string.IsNullOrEmpty(text)", we want "text"
        // For "IsGreen(text)", we want "text"
        var identifiers = expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id =>
            {
                // Skip if it's the name part of a member access (right side)
                if (id.Parent is MemberAccessExpressionSyntax ma && ma.Name == id)
                    return false;

                // Skip if it's the expression part (left side) of a member access to a type
                if (id.Parent is MemberAccessExpressionSyntax)
                    return false;

                // Skip if it's an invocation expression (method name)
                if (id.Parent is InvocationExpressionSyntax)
                    return false;

                return true;
            })
            .ToList();

        // Default to "text" if we find an identifier, otherwise "value"
        return identifiers.Count > 0 ? identifiers[0].Identifier.ValueText : "text";
    }

    private static string ReplaceInstanceMethodCalls(string expressionText)
    {
        // Simple pattern-based replacement: MethodName( -> instance.MethodName(
        // This handles cases like "IsGreen(text)" -> "instance.IsGreen(text)"
        var parts = expressionText.Split('(');
        if (parts.Length < 2)
            return expressionText;

        var methodPart = parts[0].Trim();
        // Check if it's a simple identifier (not already qualified)
        if (!methodPart.Contains('.') && char.IsUpper(methodPart[0]))
        {
            return $"instance.{expressionText}";
        }

        return expressionText;
    }

    /// <summary>
    /// Creates a composed specification with decomposed clauses and a nested model record.
    /// </summary>
    public static TypeDeclarationSyntax CreateComposed(
        string propositionName,
        string modelName,
        string recordParameters,
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        string compositionExpression)
    {
        var param = new ComposedSpecParams(
            propositionName,
            modelName,
            recordParameters,
            clauses,
            compositionExpression);

        return CreateComposedInternal(param);
    }

    private static TypeDeclarationSyntax CreateComposedInternal(ComposedSpecParams param)
    {
        // Deduplicate clauses based on their transformed text (actual expression)
        var deduplicated = DeduplicateClauses(param.Clauses);

        // Update composition expression to use deduplicated clause names and convert to camelCase
        var updatedComposition = UpdateCompositionWithCamelCaseNames(
            param.CompositionExpression,
            param.Clauses,
            deduplicated.ClauseNameMapping);

        var sb = new System.Text.StringBuilder();

        // Generate class declaration with factory lambda
        sb.AppendLine($"public class {param.PropositionName}() : Spec<{param.PropositionName}.{param.ModelName}>(() =>");
        sb.AppendLine("{");

        // Generate local variables for each clause
        foreach (var (original, transformed, _, derivedName) in deduplicated.UniqueClauses.Values)
        {
            var camelCaseName = ToCamelCase(derivedName);
            sb.AppendLine($"    var {camelCaseName} = Spec.Build(({param.ModelName} m) => {transformed})");
            sb.AppendLine($"        .WhenTrue(\"{original.EscapeDoubleQuotes()} == true\")");
            sb.AppendLine($"        .WhenFalse(\"{original.EscapeDoubleQuotes()} == false\")");
            sb.AppendLine($"        .Create();");
            sb.AppendLine();
        }

        // Return composition expression
        sb.AppendLine($"    return {updatedComposition};");
        sb.AppendLine("})");
        sb.AppendLine("{");
        sb.AppendLine($"    public record {param.ModelName}({param.RecordParameters});");
        sb.AppendLine("}");

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(sb.ToString());
        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    /// <summary>
    /// Updates the composition expression to replace clause references with camelCase names.
    /// </summary>
    private static string UpdateCompositionWithCamelCaseNames(
        string compositionExpression,
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        Dictionary<int, string> clauseNameMapping)
    {
        var result = compositionExpression;

        // Build replacement map: both Clause{N} and PascalCase derived names -> camelCase
        for (var i = 0; i < clauses.Count; i++)
        {
            var originalClauseName = $"Clause{i + 1}";
            var pascalCaseName = clauseNameMapping[i];
            var camelCaseName = ToCamelCase(pascalCaseName);

            // Replace both "Clause1" and "IsAgePositive" with "isAgePositive"
            result = result.Replace(originalClauseName, camelCaseName);
            result = result.Replace(pascalCaseName, camelCaseName);
        }

        return result;
    }

    /// <summary>
    /// Converts a PascalCase string to camelCase.
    /// </summary>
    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        return char.ToLower(pascalCase[0]) + pascalCase.Substring(1);
    }

    private static TypeDeclarationSyntax CreateInternal(
        string propositionName,
        string modelParameterName,
        ExpressionSyntax transformedExpression,
        ExpressionSyntax originalExpression,
        string modelTypeName)
    {
        var camelCasedModelParameterName = modelParameterName.ToCamelCase();
        var escapedOriginalExpression = originalExpression.ToString().EscapeDoubleQuotes();
        var propositionSource =
            $$"""
              public class {{propositionName}}() : Spec<{{modelTypeName}}>(() =>
                  Spec.Build(({{modelTypeName}} {{camelCasedModelParameterName}}) => {{transformedExpression}})
                      .WhenTrue("({{escapedOriginalExpression}}) == true")
                      .WhenFalse("({{escapedOriginalExpression}}) == false")
                      .Create());
              """;

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(propositionSource);

        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }
}
