using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.Model;
using Motiv.Generator.FluentBuilder.Model.Methods;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.Shared;

public static class FluentStepCreationExpression
{
    public  static ObjectCreationExpressionSyntax Create(
        INamespaceSymbol currentNamespace,
        IFluentMethod method,
        IEnumerable<ArgumentSyntax> arguments)
    {
        return method switch
        {
            MultiMethod multiMethod => CreateMultiMethod(currentNamespace, multiMethod, arguments),
            _ => CreateDefaultMethod(currentNamespace, method, arguments)
        };
    }

    private static ObjectCreationExpressionSyntax CreateMultiMethod(
        INamespaceSymbol currentNamespace,
        MultiMethod method,
        IEnumerable<ArgumentSyntax> arguments)
    {
        var typeArgMappings = GenericAnalysis
            .GetGenericParameterMappings(method.SourceParameter.Type, method.ParameterConverter.ReturnType)
            .ToDictionary(pair => new FluentType(pair.Key), pair => pair.Value);

        var name = IdentifierName(
            method.Return.IdentifierDisplayString(currentNamespace, typeArgMappings));

        return CreateMethodOverloadExpression(method, arguments, name);
    }

    private static ObjectCreationExpressionSyntax CreateDefaultMethod(
        INamespaceSymbol currentNamespace,
        IFluentMethod method,
        IEnumerable<ArgumentSyntax> arguments)
    {
        NameSyntax name = IdentifierName(method.Return.IdentifierDisplayString(currentNamespace));
        return CreateObjectCreationExpression(arguments, name);
    }

    private static ObjectCreationExpressionSyntax CreateObjectCreationExpression(IEnumerable<ArgumentSyntax> arguments, NameSyntax name)
    {
        return ObjectCreationExpression(name)
            .WithNewKeyword(
                Token(SyntaxKind.NewKeyword))
            .WithArgumentList(ArgumentList(SeparatedList(arguments)));
    }

    private static ObjectCreationExpressionSyntax CreateMethodOverloadExpression(
        MultiMethod method,
        IEnumerable<ArgumentSyntax> arguments,
        TypeSyntax name)
    {
        var argumentList = arguments.ToList();
        var parameterConverterMethod = method.ParameterConverter!;

        var fieldArgumentsIndex = argumentList.Count - method.MethodParameters.Length;
        var fieldSourcedArguments = argumentList.Take(fieldArgumentsIndex);
        var methodParameterSourcedArguments = argumentList.Skip(fieldArgumentsIndex);

        IEnumerable<ArgumentSyntax> argNodes =
        [
            ..fieldSourcedArguments,
            Argument(MultiMethodInvocationExpression.Create(parameterConverterMethod, methodParameterSourcedArguments, method.RootNamespace))
        ];

        return ObjectCreationExpression(name)
            .WithNewKeyword(
                Token(SyntaxKind.NewKeyword))
            .WithArgumentList(ArgumentList(SeparatedList(argNodes)));
    }
}
