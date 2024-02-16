using Humanizer;

namespace Karlssberg.Motiv.Poker;  
public class HasNCardsWithTheSameRankSpec(int sameRankCount) : Spec<Hand>(
    Spec.Build((Hand hand) => 
            hand.Ranks
                .Select(rank => new HasCardsWithTheSameRankSpec(sameRankCount, rank))
                .OrTogether())
        .WhenTrue(hand => $"has {sameRankCount.ToWords()} cards with the same rank")
        .WhenFalse(hand => $"there are {hand.Ranks.Count()} ranks when there should be {sameRankCount}")
        .CreateSpec($"has {sameRankCount.ToWords()} cards with the same rank"));

public class HasCardsWithTheSameRankSpec(int sameRankCount, Rank rank) : Spec<Hand>(
    Spec.Build(new IsRankSpec(rank))
        .AsNSatisfied(sameRankCount)
        .WhenTrue(hand => $"has {sameRankCount.ToWords()} {rank}s")
        .WhenFalse(results => $"there are {results.DeterminativeCount()} {rank}s when there should be {sameRankCount}")
        .CreateSpec($"has {sameRankCount.ToWords()} {rank}s")
        .ChangeModelTo<Hand>(h => h.Cards));

public class IsRankSpec(Rank rank) : Spec<Card>(
    Spec.Build<Card>(card => card.Rank == rank)
        .WhenTrue(card => $"{card} is a {rank}")
        .WhenFalse(card => $"{card} is not a {rank}")
        .CreateSpec($"is a {rank}"));