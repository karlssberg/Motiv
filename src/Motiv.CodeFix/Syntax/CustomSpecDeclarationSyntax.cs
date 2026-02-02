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
        IReadOnlyList<(string OriginalText, string TransformedText)> clauses,
        string compositionExpression)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"public class {propositionName}() : Spec<{propositionName}.{modelName}>(");
        sb.AppendLine($"    {compositionExpression})");
        sb.AppendLine("{");
        sb.AppendLine($"    public record {modelName}({recordParameters});");

        for (var i = 0; i < clauses.Count; i++)
        {
            sb.AppendLine();
            var (original, transformed) = clauses[i];
            var clauseName = $"Clause{i + 1}";
            sb.AppendLine($"    private static readonly SpecBase<{modelName}> {clauseName} =");
            sb.AppendLine($"        Spec.Build(({modelName} m) => {transformed})");
            sb.AppendLine($"            .WhenTrue(\"{original} == true\")");
            sb.AppendLine($"            .WhenFalse(\"{original} == false\")");
            sb.AppendLine($"            .Create();");
        }

        sb.AppendLine("}");

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(sb.ToString());
        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
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
