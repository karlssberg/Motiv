using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Syntax;

/// <summary>
/// Provides factory methods for creating custom specification declarations.
/// </summary>
public static class CustomSpecDeclarationSyntax
{
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
    /// Creates a composed specification with decomposed clauses and a nested model record.
    /// </summary>
    public static TypeDeclarationSyntax CreateComposed(
        string propositionName,
        string modelName,
        string recordParameters,
        IReadOnlyList<(string OriginalText, string TransformedText, Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax Expression)> clauses,
        string compositionExpression)
    {
        // Deduplicate clauses based on their transformed text (actual expression)
        var uniqueClauses = new Dictionary<string, (string OriginalText, string TransformedText, ExpressionSyntax Expression, string DerivedName)>();
        var clauseNameMapping = new Dictionary<int, string>(); // Maps original clause index to derived name

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
                // Reuse the existing derived name for this duplicate
                clauseNameMapping[i] = uniqueClauses[transformed].DerivedName;
            }
        }

        // Update composition expression to use deduplicated clause names
        var updatedComposition = UpdateCompositionWithDeduplicatedNames(compositionExpression, clauses, clauseNameMapping);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"public class {propositionName}() : Spec<{propositionName}.{modelName}>(");
        sb.AppendLine($"    {updatedComposition})");
        sb.AppendLine("{");
        sb.AppendLine($"    public record {modelName}({recordParameters});");

        foreach (var (original, transformed, expression, derivedName) in uniqueClauses.Values)
        {
            sb.AppendLine();
            sb.AppendLine($"    private static readonly SpecBase<{modelName}> {derivedName} =");
            sb.AppendLine($"        Spec.Build(({modelName} m) => {transformed})");
            sb.AppendLine($"            .WhenTrue(\"{original} == true\")");
            sb.AppendLine($"            .WhenFalse(\"{original} == false\")");
            sb.AppendLine($"            .Create();");
        }

        sb.AppendLine("}");

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(sb.ToString());
        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    /// <summary>
    /// Updates the composition expression to replace clause references with deduplicated names.
    /// </summary>
    private static string UpdateCompositionWithDeduplicatedNames(
        string compositionExpression,
        IReadOnlyList<(string OriginalText, string TransformedText, ExpressionSyntax Expression)> clauses,
        Dictionary<int, string> clauseNameMapping)
    {
        var result = compositionExpression;

        // Build replacement map: Clause{N} -> actual derived name
        for (var i = 0; i < clauses.Count; i++)
        {
            var originalClauseName = $"Clause{i + 1}";
            var actualName = clauseNameMapping[i];

            // Only replace if the names are different
            if (originalClauseName != actualName)
            {
                result = result.Replace(originalClauseName, actualName);
            }
        }

        return result;
    }

    private static TypeDeclarationSyntax CreateInternal(
        string propositionName,
        string modelParameterName,
        ExpressionSyntax transformedExpression,
        ExpressionSyntax originalExpression,
        string modelTypeName)
    {
        var camelCasedModelParameterName = modelParameterName.ToCamelCase();
        var propositionSource =
            $$"""
              public class {{propositionName}}() : Spec<{{modelTypeName}}>(() =>
                  Spec.Build(({{modelTypeName}} {{camelCasedModelParameterName}}) => {{transformedExpression}})
                      .WhenTrue("({{originalExpression}}) == true")
                      .WhenFalse("({{originalExpression}}) == false")
                      .Create());
              """;

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(propositionSource);

        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }
}
