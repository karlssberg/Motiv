using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Motiv.Explain;

/// <summary>
///     Rewrites a clause so it means the same inside the generated holder class as it did in the method.
///     <c>this</c> becomes the model's receiver, and a member named without a receiver (<c>_limit</c>,
///     <c>IsAdult(c)</c>) gets one: the receiver for an instance member, the declaring type for a static one,
///     because the holder inherits <c>object</c>'s members and an unqualified <c>Equals(a, b)</c> would find
///     those first. Everything else keeps its original text, so formatting survives.
/// </summary>
internal static class MemberAccessRewriter
{
    // Prefixed so no parameter of the user's method can share it
    public const string Receiver = "__motivThis";

    public static string Rewrite(IReadOnlyList<ExpressionSyntax> clause, string text, SemanticModel model)
    {
        var start = ClauseText.Span(clause).Start;
        var edits = clause.SelectMany(operand => operand.DescendantNodesAndSelf())
            .Select(node => EditFor(node, model))
            .Where(edit => edit is not null)
            .Select(edit => edit!.Value)
            .OrderByDescending(edit => edit.Start);

        foreach (var (position, length, replacement) in edits)
        {
            var offset = position - start;
            text = text.Remove(offset, length).Insert(offset, replacement);
        }

        return text;
    }

    private static (int Start, int Length, string Replacement)? EditFor(SyntaxNode node, SemanticModel model) => node switch
    {
        ThisExpressionSyntax self => (self.SpanStart, self.Span.Length, Receiver),
        SimpleNameSyntax name when ReadsImplicitThis(name, model) => (name.SpanStart, 0, Receiver + "."),
        SimpleNameSyntax name when UnqualifiedStaticMember(name, model) is { } type => (name.SpanStart, 0, type + "."),
        _ => null
    };

    // Asks the compiler rather than guessing from the syntax around the name: a name in a property pattern
    // or an object initializer also has an implicit receiver, but it is the pattern input or the new object
    private static bool ReadsImplicitThis(SimpleNameSyntax name, SemanticModel model)
    {
        var operation = name.Parent is InvocationExpressionSyntax invocation && invocation.Expression == name
            ? model.GetOperation(invocation)
            : model.GetOperation(name);

        var instance = operation switch
        {
            IInvocationOperation call => call.Instance,
            IMemberReferenceOperation reference => reference.Instance,
            _ => null
        };

        return instance is IInstanceReferenceOperation { IsImplicit: true, ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };
    }

    // The declaring type of a static member the clause names on its own, or null for anything else
    private static string? UnqualifiedStaticMember(SimpleNameSyntax name, SemanticModel model)
    {
        if (IsQualified(name) ||
            model.GetSymbolInfo(name).Symbol is not { IsStatic: true, ContainingType: { } type } symbol ||
            symbol is not (IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }))
            return null;

        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool IsQualified(SimpleNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax access => access.Name == name,
        MemberBindingExpressionSyntax => true,
        QualifiedNameSyntax qualified => qualified.Right == name,
        AliasQualifiedNameSyntax => true,
        _ => false
    };
}
