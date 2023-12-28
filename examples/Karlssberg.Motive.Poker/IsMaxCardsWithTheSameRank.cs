namespace Karlssberg.Motive.Poker;

public class IsMaxCardsWithTheSameRank(int sameRankCount) : Spec<Hand>(
    hand => hand.Ranks.GroupBy(rank => rank).Max(MeasureSize) == sameRankCount,
    $"Has {sameRankCount} cards with the same rank",
    $"Does not have {sameRankCount} cards with the same rank")
{
    private static int MeasureSize(IEnumerable<Rank> collection) => collection.Count();
}