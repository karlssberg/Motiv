namespace Karlssberg.Motive.Poker;

public class HasNCardsWithTheSameRankSpec(int sameRankCount) : Spec<Hand>(
    hand => hand.Ranks.GroupBy(rank => rank).Any(collection => collection.Count() == sameRankCount),
    $"Has {sameRankCount} cards with the same rank",
    $"Does not have {sameRankCount} cards with the same rank");