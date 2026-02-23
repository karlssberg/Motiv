using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Provides factory methods for creating custom specification declarations.
/// </summary>
public static class CustomSpecDeclarationSyntax
{
    /// <summary>
    ///     Creates a type declaration for a custom specification.
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
    ///     Creates a constructor-based specification for expressions with instance method calls.
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

        var sb = new StringBuilder();

        // Generate class declaration with primary constructor and factory lambda
        sb.AppendLine(
            $"public class {param.PropositionName}({param.ContainingTypeName} instance) : Spec<{fullModelType}>(() =>");
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
                // For record model, use transformed text but also replace instance methods
                lambdaBody = hasInstanceMethod
                    ? ReplaceInstanceMethodCalls(transformed)
                    : transformed;
            else
                lambdaBody = hasInstanceMethod
                    ? ReplaceInstanceMethodCalls(expression.ToString())
                    : expression.ToString();

            var paramName = hasRecordModel ? "m" : GetParameterNameFromExpression(expression);

            sb.AppendLine($"    var {camelCaseName} = Spec.Build(({modelType} {paramName}) => {lambdaBody})");
            sb.AppendLine($"        .Create((\"{original.EscapeDoubleQuotes()}\");");
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

        var compilationUnit = ParseCompilationUnit(sb.ToString());
        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    /// <summary>
    ///     Deduplicates clauses based on their transformed text, assigning derived names.
    /// </summary>
    private static DeduplicatedClauses DeduplicateClauses(
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses)
    {
        var uniqueClauses =
            new Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string
                DerivedName)>();
        var clauseNameMapping = new Dictionary<int, string>();

        for (var i = 0; i < clauses.Count; i++)
        {
            var (original, transformed, expression) = clauses[i];

            if (!uniqueClauses.TryGetValue(transformed, out var clause))
            {
                var derivedName = ClauseNameDeriver.DeriveName(expression, uniqueClauses.Count + 1);
                uniqueClauses[transformed] = (original, transformed, expression, derivedName);
                clauseNameMapping[i] = derivedName;
            }
            else
            {
                clauseNameMapping[i] = clause.DerivedName;
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
                return id.Parent switch
                {
                    // Skip if it's the name part of a member access (right side)
                    MemberAccessExpressionSyntax ma when ma.Name == id => false,

                    // Skip if it's the expression part (left side) of a member access to a type
                    MemberAccessExpressionSyntax => false,

                    // Skip if it's an invocation expression (method name)
                    InvocationExpressionSyntax => false,

                    _ => true
                };
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
        if (!methodPart.Contains('.') && char.IsUpper(methodPart[0])) return $"instance.{expressionText}";

        return expressionText;
    }

    /// <summary>
    ///     Creates a composed specification with decomposed clauses and a nested model record.
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

        var sb = new StringBuilder();

        // Generate class declaration with factory lambda
        sb.AppendLine(
            $"public class {param.PropositionName}() : Spec<{param.PropositionName}.{param.ModelName}>(() =>");
        sb.AppendLine("{");

        // Generate local variables for each clause
        foreach (var (original, transformed, _, derivedName) in deduplicated.UniqueClauses.Values)
        {
            // Build the fluent chain for this clause
            var specChain = BuildSpecFluentChain(
                param.ModelName,
                "m",
                ParseExpression(transformed),
                original);

            // var clause1 = Spec.Build((Model m) => true)
            var assignment = LocalDeclarationStatement(
                VariableDeclaration(
                    IdentifierName("var"))
                .WithVariables(
                    SingletonSeparatedList(
                        VariableDeclarator(
                            Identifier(ToCamelCase(derivedName)))
                        .WithInitializer(
                            EqualsValueClause(specChain)))))

                .WithTrailingTrivia(CarriageReturnLineFeed);

            sb.AppendLine(assignment.ToFullString());
        }

        // Return composition expression
        sb.AppendLine($"    return {updatedComposition};");
        sb.AppendLine("})");
        sb.AppendLine("{");
        sb.AppendLine($"    public record {param.ModelName}({param.RecordParameters});");
        sb.AppendLine("}");

        var compilationUnit = ParseCompilationUnit(sb.ToString());
        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    /// <summary>
    ///     Updates the composition expression to replace clause references with camelCase names.
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
    ///     Converts a PascalCase string to camelCase.
    /// </summary>
    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        return char.ToLower(pascalCase[0]) + pascalCase.Substring(1);
    }

    /// <summary>
    ///     Builds a Spec.Build(...).Create(...) fluent chain expression.
    /// </summary>
    private static ExpressionSyntax BuildSpecFluentChain(
        string modelTypeName,
        string parameterName,
        ExpressionSyntax bodyExpression,
        string escapedOriginalExpression)
    {
        // 1. Build inner lambda: (modelTypeName parameterName) => bodyExpression
        var innerLambda = ParenthesizedLambdaExpression(
            ParameterList(
                SingletonSeparatedList(
                    Parameter(Identifier(parameterName))
                        .WithType(IdentifierName(modelTypeName)))),
            bodyExpression);

        // 2. Build Spec.Build(innerLambda)
        var specBuildInvocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("Spec"),
                IdentifierName("Build")),
            ArgumentList(
                SingletonSeparatedList(
                    Argument(innerLambda))))
            .WithTrailingTrivia(CarriageReturnLineFeed);

        // 3. Chain .Create(...)
        var createInvocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                specBuildInvocation,
                IdentifierName("Create")),
            ArgumentList(
                SingletonSeparatedList(
                    Argument(
                        LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal($"{escapedOriginalExpression}"))))))
            .WithTrailingTrivia(CarriageReturnLineFeed);

        return createInvocation;
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

        // 1. Build the fluent chain: Spec.Build(...).WhenTrue(...).WhenFalse(...).Create()
        var fluentChain = BuildSpecFluentChain(
            modelTypeName,
            camelCasedModelParameterName,
            transformedExpression,
            escapedOriginalExpression);

        // 2. Build outer lambda: () => fluentChain
        var outerLambda = ParenthesizedLambdaExpression(
            ParameterList(),
            fluentChain);

        // 3. Build base type: Spec<modelTypeName>(() => fluentChain)
        var baseType = PrimaryConstructorBaseType(
            GenericName(
                Identifier("Spec"),
                TypeArgumentList(
                    SingletonSeparatedList<TypeSyntax>(
                        IdentifierName(modelTypeName)))),
            ArgumentList(
                SingletonSeparatedList(
                    Argument(outerLambda))));

        // 4. Build class declaration: public class propositionName() : baseType;
        var classDeclaration = ClassDeclaration(propositionName)
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                ParameterList())
            .WithBaseList(
                BaseList(
                    SingletonSeparatedList<BaseTypeSyntax>(baseType)))
            .WithSemicolonToken(
                Token(SyntaxKind.SemicolonToken));

        return classDeclaration;
    }

    /// <summary>
    ///     Parameters for creating a composed specification with constructor.
    /// </summary>
    private readonly struct ConstructorSpecParams(
        string propositionName,
        string modelName,
        string? singleModelTypeName,
        string? recordParameters,
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        string compositionExpression,
        string containingTypeName)
    {
        public string PropositionName { get; } = propositionName;
        public string ModelName { get; } = modelName;
        public string? SingleModelTypeName { get; } = singleModelTypeName;
        public string? RecordParameters { get; } = recordParameters;

        public IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> Clauses
        {
            get;
        } = clauses;

        public string CompositionExpression { get; } = compositionExpression;
        public string ContainingTypeName { get; } = containingTypeName;
    }

    /// <summary>
    ///     Parameters for creating a composed specification.
    /// </summary>
    private readonly struct ComposedSpecParams(
        string propositionName,
        string modelName,
        string recordParameters,
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        string compositionExpression)
    {
        public string PropositionName { get; } = propositionName;
        public string ModelName { get; } = modelName;
        public string RecordParameters { get; } = recordParameters;

        public IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> Clauses
        {
            get;
        } = clauses;

        public string CompositionExpression { get; } = compositionExpression;
    }

    /// <summary>
    ///     Result of clause deduplication containing unique clauses and name mappings.
    /// </summary>
    private readonly struct DeduplicatedClauses(
        Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string DerivedName)> uniqueClauses,
        Dictionary<int, string> clauseNameMapping)
    {
        public Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string
            DerivedName)> UniqueClauses { get; } = uniqueClauses;

        public Dictionary<int, string> ClauseNameMapping { get; } = clauseNameMapping;
    }
}
