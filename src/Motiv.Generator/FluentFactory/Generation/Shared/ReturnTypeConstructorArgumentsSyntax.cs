using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Storage;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.Shared;

public static class ReturnTypeConstructorArgumentsSyntax
{
    public static IEnumerable<ArgumentSyntax> Create(IFluentMethod method)
    {
        return method.ValueSources
            .Select(pair => pair.Value)
            .Select(storage =>
            {
                ExpressionSyntax node =
                    storage switch
                    {
                        PrimaryConstructorParameterStorage =>
                            IdentifierName(storage.IdentifierName),
                        FieldStorage or PropertyStorage =>
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                IdentifierName(storage.IdentifierName)),
                        _ =>
                            DefaultExpression(
                                ParseTypeName(
                                    storage.Type.ToDynamicDisplayString(method.RootNamespace)))
                    };

                return Argument(node);
            })
            .Concat(method.MethodParameters
                .Select(p => p.ParameterSymbol.Name.ToCamelCase())
                .Select(IdentifierName)
                .Select(Argument));
    }
}
