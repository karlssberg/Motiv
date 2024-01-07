namespace Karlssberg.Motiv.Poker;

public class HasNCardsWithTheSameRankSpec(int sameRankCount) : Spec<Hand>(
    Spec.Build<Hand>(hand => hand.Ranks.GroupBy(rank => rank).Any(collection => collection.Count() == sameRankCount))
        .YieldWhenTrue($"has {sameRankCount} cards with the same rank")
        .YieldWhenFalse($"does not have {sameRankCount} cards with the same rank")
        .CreateSpec());