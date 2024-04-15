
namespace Karlssberg.Motiv.Poker;

public class HasNCardsWithTheSameRankProposition(int sameRankCount) : Spec<Hand>(
    Spec.Build((Hand hand) =>
            hand.Ranks
                .Select(rank => new HasNCardsWithTheSameRank(sameRankCount, rank))
                .OrTogether()
                .IsSatisfiedBy(hand.Cards))
        .Create($"has {sameRankCount} card(s) with the same rank"))
{
    private class HasNCardsWithTheSameRank(int sameRankCount, Rank rank) : Spec<IEnumerable<Card>>(
        Spec.Build((Card card) => card.Rank == rank)
            .AsNSatisfied(sameRankCount)
            .Create($"has {sameRankCount} {rank}(s)"));
}

