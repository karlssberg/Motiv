namespace Karlssberg.Motive;

public static class BooleanResultExtensions
{
    public static void Accept<TMetadata, TVisitor>(this IEnumerable<BooleanResultBase<TMetadata>> booleanResults, TVisitor visitor)
        where TVisitor : IBooleanResultVisitor<TMetadata>
    {
        foreach (var booleanResult in booleanResults)
        {
            booleanResult.Accept(visitor);
        }
    }
}