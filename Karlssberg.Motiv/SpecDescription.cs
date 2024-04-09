namespace Karlssberg.Motiv;

internal sealed class SpecDescription(string statement, ISpecDescription? underlyingProposition = null) : ISpecDescription
{
    public string Statement => statement;

    public string Detailed =>
        underlyingProposition switch
        {
            null => statement,
            not null =>
                $$"""
                  {{statement}} {
                      {{underlyingProposition.Detailed.IndentAfterFirstLine()}}
                  }
                  """
        };

    public override string ToString() => Statement;
}
//
//internal sealed class AssertionProposition(string statement, string truIProposition? underlyingProposition = null) : IProposition
//{
//    public string Statement => statement;
//
//    public string Detailed =>
//        underlyingProposition switch
//        {
//            null => statement,
//            not null =>
//                $$"""
//                  {{statement}} {
//                      {{underlyingProposition.Detailed.IndentAfterFirstLine()}}
//                  }
//                  """
//        };
//
//    public override string ToString() => Statement;
//}