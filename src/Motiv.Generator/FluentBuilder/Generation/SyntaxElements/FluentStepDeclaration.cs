using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Constructors;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Methods;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.ValueStorage;
using Motiv.Generator.FluentBuilder.Model.Methods;
using Motiv.Generator.FluentBuilder.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements;

public static class FluentStepDeclaration
{
    public static StructDeclarationSyntax Create(
        RegularFluentStep step)
    {
        var methodDeclarationSyntaxes = step.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method =>
                method switch
                {
                    CreateMethod createMethod => FluentFactoryMethodDeclaration.Create(createMethod, step),
                    MultiMethod multiMethod => FluentStepMethodDeclaration.Create(multiMethod, step.KnownConstructorParameters, step.Namespace),
                    _ => FluentStepMethodDeclaration.Create(method, step.KnownConstructorParameters, step.Namespace)
                });

        var fieldDeclarations = FieldAndPropertySyntax.CreateDeclarations(step.ValueStorage);

        var constructor = FluentStepConstructorDeclaration.Create(step);

        NameSyntax name = IdentifierName(((IFluentReturn)step).IdentifierDisplayString(step.Namespace));

        var identifier = name is GenericNameSyntax genericName
            ? genericName.Identifier
            : ((SimpleNameSyntax)name).Identifier;

        SyntaxTokenList accessibilityToken = step.Accessibility switch
        {
            Accessibility.Public => [Token(SyntaxKind.PublicKeyword)],
            Accessibility.Private => [Token(SyntaxKind.PrivateKeyword)],
            Accessibility.Protected => [Token(SyntaxKind.ProtectedKeyword)],
            Accessibility.Internal => [Token(SyntaxKind.InternalKeyword)],
            Accessibility.ProtectedOrInternal => [Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.InternalKeyword)],
            Accessibility.ProtectedAndInternal => [Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword)],
            _ => [Token(SyntaxKind.PublicKeyword)]
        };

        // Create XML documentation for the struct
        var xmlDocTrivia = CreateXmlDocumentation(step.Name);

            var structDeclaration = StructDeclaration(identifier)
            .WithModifiers(accessibilityToken)
            .WithMembers(List<MemberDeclarationSyntax>([
                ..fieldDeclarations,
                constructor,
                ..methodDeclarationSyntaxes,
            ]));

                return structDeclaration;
    }

    /// <summary>
    /// Creates XML documentation trivia for a fluent step struct declaration.
    /// </summary>
    /// <param name="stepName">The name of the step to document.</param>
    /// <returns>A SyntaxTriviaList containing the XML documentation.</returns>
    private static SyntaxTriviaList CreateXmlDocumentation(string stepName)
    {
        // Using a fixed "\n" instead of Environment.NewLine to avoid analyzer warning RS1035
        const string newLine = "\n";
        string docText = $" Represents a fluent builder step for {stepName}.";
        
        return TriviaList(
            Trivia(
                DocumentationCommentTrivia(
                    SyntaxKind.SingleLineDocumentationCommentTrivia,
                    List<XmlNodeSyntax>(
                        [
                            XmlText()
                                .WithTextTokens(
                                    TokenList(
                                        XmlTextLiteral(
                                            TriviaList(
                                                DocumentationCommentExterior("///")),
                                            " ",
                                            " ",
                                            TriviaList()))),
                            XmlElement(
                                XmlElementStartTag(
                                    XmlName(
                                        Identifier("summary"))),
                                XmlElementEndTag(
                                    XmlName(
                                        Identifier("summary"))))
                                .WithContent(
                                    SingletonList<XmlNodeSyntax>(
                                        XmlText()
                                            .WithTextTokens(
                                                TokenList(
                                                    XmlTextNewLine(
                                                        TriviaList(),
                                                        newLine,
                                                        newLine,
                                                        TriviaList()),
                                                    XmlTextLiteral(
                                                        TriviaList(
                                                            DocumentationCommentExterior("    ///")),
                                                        docText,
                                                        docText,
                                                        TriviaList()),
                                                    XmlTextNewLine(
                                                        TriviaList(),
                                                        newLine,
                                                        newLine,
                                                        TriviaList()),
                                                    XmlTextLiteral(
                                                        TriviaList(
                                                            DocumentationCommentExterior("    ///")),
                                                        " ",
                                                        " ",
                                                        TriviaList()))))),
                            XmlText()
                                .WithTextTokens(
                                    TokenList(
                                        XmlTextNewLine(
                                            TriviaList(),
                                            newLine,
                                            newLine,
                                            TriviaList())))
                        ]))));
    }
}
