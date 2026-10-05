using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.Explain;

/// <summary>
///     Splits a boolean expression at its logical operators and renders the Motiv composition that
///     recombines the pieces, with the same evaluation order and short-circuiting as the source.
/// </summary>
/// <remarks>
///     <para>
///         Each clause becomes its own delegate, so unlike <c>Spec.From</c> a clause may hold anything a lambda
///         can: patterns, switch expressions, <c>?.</c>. An operator is only split when its operands are
///         <c>bool</c>, so user-defined and bitwise operators stay whole.
///     </para>
///     <para>
///         A clause that reads a variable an earlier one declares (<c>o is Foo f &amp;&amp; f.Ok</c>) can't live
///         in a separate delegate, so the two stay together. For <c>&amp;&amp;</c> and <c>||</c> chains only the
///         operands from the declaration to its last use are kept together, since regrouping those operators
///         changes neither the answer nor what gets evaluated.
///     </para>
/// </remarks>
internal sealed class ClauseDecomposer(SemanticModel model, Func<IReadOnlyList<ExpressionSyntax>, string> renderClause)
{
    public string Render(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => Render(parenthesized.Expression),

        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not when IsBool(not.Operand)
            => $"{Render(not.Operand)}.Not()",

        BinaryExpressionSyntax binary when CombinatorFor(binary) is { } combinator
            => RenderChain(Flatten(binary), combinator),

        _ => renderClause([expression])
    };

    private string RenderChain(IReadOnlyList<ExpressionSyntax> operands, string combinator) =>
        string.Join("", Segments(operands).Select((segment, index) =>
        {
            var rendered = segment.Count == 1 ? Render(segment[0]) : renderClause(segment);
            return index == 0 ? rendered : $".{combinator}({rendered})";
        }));

    private string? CombinatorFor(BinaryExpressionSyntax binary)
    {
        var combinator = binary.Kind() switch
        {
            SyntaxKind.LogicalAndExpression => "AndAlso",
            SyntaxKind.LogicalOrExpression => "OrElse",
            SyntaxKind.BitwiseAndExpression => "And",
            SyntaxKind.BitwiseOrExpression => "Or",
            SyntaxKind.ExclusiveOrExpression => "XOr",
            _ => null
        };

        return combinator is not null && IsBool(binary.Left) && IsBool(binary.Right) ? combinator : null;
    }

    // `a && b && c` parses as `(a && b) && c`; a chain of && or || is flattened so it can be regrouped.
    // The eager operators keep their two operands, which then only ever split or stay whole.
    private static IReadOnlyList<ExpressionSyntax> Flatten(BinaryExpressionSyntax binary)
    {
        if (!binary.IsKind(SyntaxKind.LogicalAndExpression) && !binary.IsKind(SyntaxKind.LogicalOrExpression))
            return [binary.Left, binary.Right];

        var left = binary.Left is BinaryExpressionSyntax inner && inner.IsKind(binary.Kind())
            ? Flatten(inner)
            : [binary.Left];
        return [.. left, binary.Right];
    }

    // Consecutive operands, each run reaching from a variable's declaration to its last use
    private IEnumerable<IReadOnlyList<ExpressionSyntax>> Segments(IReadOnlyList<ExpressionSyntax> operands)
    {
        for (var start = 0; start < operands.Count;)
        {
            var end = start;
            for (var index = start; index <= end; index++)
                end = Math.Max(end, LastUseOfVariablesDeclaredIn(operands, index));

            yield return operands.Skip(start).Take(end - start + 1).ToList();
            start = end + 1;
        }
    }

    private int LastUseOfVariablesDeclaredIn(IReadOnlyList<ExpressionSyntax> operands, int index)
    {
        var declared = operands[index].DescendantNodesAndSelf()
            .OfType<SingleVariableDesignationSyntax>()
            .Select(designation => model.GetDeclaredSymbol(designation))
            .Where(symbol => symbol is not null)
            .ToList();
        if (declared.Count == 0)
            return index;

        for (var later = operands.Count - 1; later > index; later--)
        {
            var usesOne = operands[later].DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(name => declared.Contains(model.GetSymbolInfo(name).Symbol, SymbolEqualityComparer.Default));
            if (usesOne)
                return later;
        }

        return index;
    }

    private bool IsBool(ExpressionSyntax expression) =>
        model.GetTypeInfo(expression).Type?.SpecialType == SpecialType.System_Boolean;
}
