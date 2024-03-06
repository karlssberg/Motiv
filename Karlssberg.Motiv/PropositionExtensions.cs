

using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv;

internal static class PropositionExtensions
{
//    internal static string SerializeWithIndent<TModel, TMetadata>(this SpecBase<TModel, TMetadata> spec, int indentLevel = 0)
//    {
//        return spec switch
//        {
//            AndSpec<TModel, TMetadata> andSpec => $"({andSpec.Description})".Indent(indentLevel),
//            OrSpec<TModel, TMetadata> orSpec => $"({orSpec.Description})".Indent(indentLevel),
//            XOrSpec<TModel, TMetadata> xorSpec => $"({xorSpec.Description})".Indent(indentLevel),
//            ElseIfSpec<TModel, TMetadata> elseIfSpec => $"({elseIfSpec.Description})".Indent(indentLevel),
//            _ when IsAlreadyEncapsulated(spec.Description) => spec.Description.Indent(indentLevel),
//            _ => $"({spec.Description})".Indent(indentLevel)
//        };
//        
//        static bool IsAlreadyEncapsulated(string text) => 
//            (text.StartsWith("(") || text.StartsWith("!(")) && text.EndsWith(")");
//    }
    private const int Spaces = 4;    

    internal static string IndentAfterFirstLine(this string text, int levels = 1)
    {
        var indentation = string.Join("", Enumerable.Repeat(" ", Spaces * levels));
        return text.Replace("\n", $"\n{indentation}");
    }
    
    internal static string Indent(this string text, int levels = 1)
    {
        var indentation = string.Join("", Enumerable.Repeat(" ", Spaces * levels));
        return $"{indentation}{text.Replace("\n", $"\n{indentation}")}";
    }
    
    internal static string JoinLines(this IEnumerable<string> textCollection) => 
        string.Join(Environment.NewLine, textCollection);

    internal static string ToReason<TMetadata>(
        this IProposition proposition,
        bool isSatisfied,
        TMetadata metadata,
        AssertionSource assertionSource = AssertionSource.Unknown)
    {
        return (isSatisfied, metadata, assertionSource: assertionSource) switch
        {
            (_, string reason, not AssertionSource.Proposition) => reason,
            (true, _, _) when PropositionContains('!') => $"({proposition.Assertion})",
            (true, _, _) => proposition.Assertion,
            (false, _, _) when PropositionContains('!') => $"!({proposition.Assertion})",
            (false, _, _) => $"!{proposition.Assertion}",
        };
        
        bool PropositionContains(char ch) => proposition.Assertion.Contains(ch);
    }
}