using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Motiv.Explain;

/// <summary>
///     Rewrites a clause of an instance method so it can run inside a static delegate: <c>this</c> becomes
///     <c>@this</c>, and a member read through an implicit <c>this</c> (<c>_limit</c>, <c>IsAdult(c)</c>) gets
///     the <c>@this.</c> it was missing. Everything else keeps its original text, so formatting survives.
/// </summary>
internal static class InstanceAccessRewriter
{
    public const string Receiver = "@this";

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
}
