using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Motiv.CodeFix.Syntax;

public static class CustomSpecDeclarationSyntax
{
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

    public static TypeDeclarationSyntax Create(
        IdentifierNameSyntax propositionName,
        IdentifierNameSyntax modelParameterName,
        ExpressionSyntax transformedExpression,
        ExpressionSyntax originalExpression,
        string modelTypeName)
    {
        return CreateInternal(
            propositionName.ToString(),
            modelParameterName.ToString(),
            transformedExpression,
            originalExpression,
            modelTypeName);
    }

    public static TypeDeclarationSyntax Create(
        GenericNameSyntax propositionName,
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


    private static string TransformExpressionForModel(ExpressionSyntax logicalExpression, string modelParameterName)
    {
        // Use simple string replacement to transform variable references to model property references
        var expressionText = logicalExpression.ToString();

        // Find all identifier names that are likely variables and transform them
        var identifiers = logicalExpression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id =>
                // Not part of member access (like obj.property)
                !(id.Parent is MemberAccessExpressionSyntax) &&
                // Not method names in invocations
                !(id.Parent is InvocationExpressionSyntax) &&
                // Not type names in object creation
                !(id.Parent is ObjectCreationExpressionSyntax))
            .Select(id => id.Identifier.ValueText)
            .Distinct()
            .ToList();

        // Only transform identifiers that look like parameter/variable names (lowercase start)
        foreach (var identifier in identifiers.Where(id => char.IsLower(id[0])))
        {
            var propertyName = identifier.Capitalize();
            // Use word boundary replacement to avoid partial matches
            expressionText = System.Text.RegularExpressions.Regex.Replace(
                expressionText,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(identifier)}\b",
                $"{modelParameterName}.{propertyName}");
        }

        return expressionText;
    }
}
