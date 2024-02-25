

using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

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
        ReasonSource reasonSource = ReasonSource.Unknown)
    {
        return (isSatisfied, metadata, reasonSource) switch
        {
            (_, string reason, not ReasonSource.Proposition) => reason,
            (true, _, _) when PropositionContains('!') => $"({proposition.Name})",
            (true, _, _) => proposition.Name,
            (false, _, _) when PropositionContains('!') => $"!({proposition.Name})",
            (false, _, _) => $"!{proposition.Name}",
        };
        
        bool PropositionContains(char ch) => proposition.Name.Contains(ch);
    }
}