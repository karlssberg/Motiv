using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.Explain;

/// <summary>Why an <c>[Explain]</c> method can't be explained, as the analyzer reports it.</summary>
internal sealed record Rejection(DiagnosticDescriptor Rule, Location Location, object[] Arguments)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Rule, Location, Arguments);
}

/// <summary>
///     The one place that decides whether an <c>[Explain]</c> method can be explained. The generator skips
///     a method this rejects, and the analyzer reports the rejection, so the two can never disagree.
/// </summary>
internal static class ExplainEligibility
{
    public static Rejection? Check(IMethodSymbol method, SyntaxNode declaration, Compilation compilation)
    {
        var location = declaration switch
        {
            MethodDeclarationSyntax m => m.Identifier.GetLocation(),
            LocalFunctionStatementSyntax f => f.Identifier.GetLocation(),
            _ => declaration.GetLocation()
        };

        var reason = FindReason(method, declaration, compilation);
        if (reason is not null)
            return new Rejection(ExplainDiagnostics.CannotExplain, location, [method.ToDisplayString(), reason]);

        var notPartial = ContainingTypes(method).FirstOrDefault(type => !IsPartial(type));
        return notPartial is null
            ? null
            : new Rejection(ExplainDiagnostics.TypeNotPartial, location, [method.ToDisplayString(), notPartial.ToDisplayString()]);
    }

    /// <summary>The expression to explain when the method can be explained, otherwise null.</summary>
    public static ExpressionSyntax? Accept(IMethodSymbol method, SyntaxNode declaration, Compilation compilation) =>
        Check(method, declaration, compilation) is null ? FindBody(declaration) : null;

    // The single boolean expression the method returns, or null when its body is anything else
    private static ExpressionSyntax? FindBody(SyntaxNode declaration) => declaration switch
    {
        BaseMethodDeclarationSyntax { ExpressionBody: { } arrow } => arrow.Expression,
        BaseMethodDeclarationSyntax { Body.Statements: { Count: 1 } statements } when statements[0] is ReturnStatementSyntax { Expression: { } returned }
            => returned,
        _ => null
    };

    private static string? FindReason(IMethodSymbol method, SyntaxNode declaration, Compilation compilation)
    {
        if (method.MethodKind == MethodKind.LocalFunction)
            return "it is a local function";
        // The generated code reopens every enclosing type as a partial class
        if (ContainingTypes(method).Any(type => type.TypeKind is not TypeKind.Class))
            return "it is declared in a struct or interface";
        if (method.IsExtensionMethod)
            return "extension methods aren't supported";
        if (method.IsGenericMethod || ContainingTypes(method).Any(type => type.IsGenericType))
            return "generic methods and types aren't supported";
        if (method.ReturnType.SpecialType != SpecialType.System_Boolean || method.ReturnsByRef || method.ReturnsByRefReadonly)
            return "it doesn't return bool";
        if (method.Parameters.Any(parameter => parameter.RefKind != RefKind.None))
            return "it has a ref, out or in parameter";
        // The parameters become a Motiv model, which is a generic argument
        if (method.Parameters.Any(parameter => parameter.Type.IsRefLikeType || parameter.Type is IPointerTypeSymbol or IFunctionPointerTypeSymbol))
            return "a parameter is a ref struct or pointer";
        if (CanBeOverridden(method))
            return "it can be overridden, so a call might not run this body";

        var body = FindBody(declaration);
        if (body is null)
            return "its body isn't a single expression";
        if (body is ThrowExpressionSyntax)
            return "its body only throws";
        if (body.DescendantNodesAndSelf().OfType<BaseExpressionSyntax>().Any())
            return "it uses base";
        // The clauses run in a nested class, which can't capture the type's primary constructor parameters
        if (ReadsPrimaryConstructorParameter(body, compilation.GetSemanticModel(body.SyntaxTree)))
            return "it reads a primary constructor parameter";

        // The generated code sits outside the type, so everything in the signature must be reachable from there
        if (!compilation.IsSymbolAccessibleWithin(method.ContainingType, compilation.Assembly))
            return "its type isn't visible to the rest of the assembly";
        if (method.Parameters.Any(parameter => !compilation.IsSymbolAccessibleWithin(parameter.Type, compilation.Assembly)))
            return "a parameter's type isn't visible to the rest of the assembly";

        return null;
    }

    private static bool ReadsPrimaryConstructorParameter(ExpressionSyntax body, SemanticModel model) =>
        body.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(name => model.GetSymbolInfo(name).Symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } });

    // A call that binds to this method could dispatch to an override at run time, which an interceptor would skip
    private static bool CanBeOverridden(IMethodSymbol method) =>
        method.IsVirtual || method.IsAbstract || (method.IsOverride && !method.IsSealed && !method.ContainingType.IsSealed);

    private static IEnumerable<INamedTypeSymbol> ContainingTypes(IMethodSymbol method)
    {
        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
            yield return type;
    }

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
}
