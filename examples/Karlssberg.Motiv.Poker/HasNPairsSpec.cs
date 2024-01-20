namespace Karlssberg.Motiv.Poker;

public class HasNPairsSpec(int pairCount) : Spec<Hand>(
    Spec.Build<Hand>(hand => hand.Ranks.GroupBy(r => r).Count(IsAPair) == pairCount)
        .YieldWhenTrue($"Has {pairCount} {PluralizePair(pairCount)}")
        .YieldWhenFalse($"Does not have {pairCount} {PluralizePair(pairCount)}")
        .CreateSpec())
{
    private static bool IsAPair(IEnumerable<Rank> g) => g.Count() == 2;
    private static string PluralizePair(int count) =>
        count == 1 ? "pair" : "pairs";
}