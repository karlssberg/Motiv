namespace Karlssberg.Motiv.Poker;

public class HasNPairsSpec(int pairCount) : Spec<Hand>(
    hand => hand.Ranks.GroupBy(r => r).Count(IsAPair) == pairCount,
    $"Has {pairCount} {PluralizePair(pairCount)}",
    $"Does not have {pairCount} {PluralizePair(pairCount)}")
{
    private static bool IsAPair(IEnumerable<Rank> g) => g.Count() == 2;
    private static string PluralizePair(int count) => 
        count == 1 ? "pair" : "pairs";
}