using Humanizer;

namespace Karlssberg.Motiv.Poker;

public class HasNCardsWithTheSameRankSpec(int sameRankCount) : Spec<Hand>(
    Spec.Build<Hand>(hand => HandHasCardsWithTheSameRank(hand, sameRankCount))
        .WhenTrue(hand => $"has {sameRankCount.ToWords()} cards with the same rank")
        .WhenFalse(hand => $"there are {hand.Ranks.Count()} ranks when there should be {sameRankCount}")
        .CreateSpec($"has {sameRankCount.ToWords()} cards with the same rank"))
{
    private static SpecBase<Hand, string> HandHasCardsWithTheSameRank(Hand hand, int sameRankCount) =>
        hand.Ranks
            .Select(rank => HasCardsWithTheSameRank(sameRankCount, rank))
            .Select(spec => spec.ChangeModelTo<Hand>(h => h.Cards))
            .OrTogether();

    private static SpecBase<IEnumerable<Card>, string> HasCardsWithTheSameRank(int sameRankCount, Rank rank) =>
        Spec.Build(IsRank(rank))
            .AsNSatisfied(sameRankCount)
            .WhenTrue(hand => $"has {sameRankCount.ToWords()} {rank}s")
            .WhenFalse(results =>
                $"there are {results.CauseCount} {rank}s when there should be {sameRankCount}")
            .CreateSpec($"has {sameRankCount.ToWords()} {rank}s");

    private static SpecBase<Card, string> IsRank(Rank rank) =>
        Spec.Build<Card>(card => card.Rank == rank)
            .WhenTrue(card => $"{card} is a {rank}")
            .WhenFalse(card => $"{card} is not a {rank}")
            .CreateSpec($"is a {rank}");
}

