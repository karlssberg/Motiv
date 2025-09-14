using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Syntax;

public static class CustomSpecDeclarationSyntax
{
    public static TypeDeclarationSyntax Create(
        IdentifierNameSyntax propositionName,
        IdentifierNameSyntax modelParameterName,
        ExpressionSyntax logicalExpression)
    {
        return CreateInternal(
            propositionName.ToString(),
            modelParameterName.ToString(),
            logicalExpression.ToString());
    }

    public static TypeDeclarationSyntax Create(
        GenericNameSyntax propositionName,
        IdentifierNameSyntax modelParameterName,
        ExpressionSyntax logicalExpression)
    {
        return CreateInternal(
            propositionName.ToString(),
            modelParameterName.ToString(),
            logicalExpression.ToString());
    }

    private static TypeDeclarationSyntax CreateInternal(
        string propositionName,
        string modelParameterName,
        string logicalExpression)
    {
        var propositionSource =
            $$"""
              public class {{propositionName}}() : Spec<PropositionModel>(() =>
                  Spec.Build((PropositionModel {{modelParameterName}}) => {{logicalExpression}})
                      .WhenTrue("({{logicalExpression}}) == true")
                      .WhenFalse("({{logicalExpression}}) == false")
                      .Create());
              """;

        var compilationUnit = SyntaxFactory.ParseCompilationUnit(propositionSource);

        return compilationUnit.DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }
}
