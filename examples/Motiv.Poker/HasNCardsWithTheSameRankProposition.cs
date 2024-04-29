
namespace Motiv.Poker;

public class HasNCardsWithTheSameRankProposition(int sameRankCount) : Spec<Hand>(
    Spec.Build((Hand hand) =>
        {
            var hasNCardsWithTheSameRank = hand.Ranks
                .Select(rank => new HasNCardsWithTheSameRank(sameRankCount, rank))
                .OrTogether();
            
            return hasNCardsWithTheSameRank.IsSatisfiedBy(hand.Cards);
        })
        .Create($"has {sameRankCount} card(s) with the same rank"))
{
    private class HasNCardsWithTheSameRank(int sameRankCount, Rank rank) : Spec<IEnumerable<Card>>(
        Spec.Build((Card card) => card.Rank == rank)
            .AsNSatisfied(sameRankCount)
            .Create($"has {sameRankCount} {rank}(s)"));
}

