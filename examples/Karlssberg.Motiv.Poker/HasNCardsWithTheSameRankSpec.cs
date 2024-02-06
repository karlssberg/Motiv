using Humanizer;

namespace Karlssberg.Motiv.Poker;  
public class HasNCardsWithTheSameRankSpec(int sameRankCount) : Spec<Hand>(
    Spec.Build<Hand>(hand => hand.Ranks
            .Select(rank => new HasCardsWithTheSameRankSpec(sameRankCount, rank))
            .OrTogether())
        .YieldWhenTrue($"has {sameRankCount} {"card".ToQuantity(sameRankCount)} with the same rank")
        .YieldWhenFalse(hand => $"has {hand.Ranks.Count()} ranks the same when there should be {sameRankCount}")
        .CreateSpec($"has {sameRankCount} {"card".ToQuantity(sameRankCount)} with the same rank"));

public class HasCardsWithTheSameRankSpec(int sameRankCount, Rank rank) : Spec<Hand>(
    new IsRankSpec(rank)
        .Exactly(sameRankCount)
        .YieldWhenTrue($"has {sameRankCount.ToWords()} {rank}s")
        .YieldWhenFalse((satisfied, _) =>
            $"there are {satisfied.Count()} {rank}s when there should be {sameRankCount}")
        .ChangeModel<Hand>(h => h.Cards));

public class IsRankSpec(Rank rank) : Spec<Card>(
    Spec.Build<Card>(card => card.Rank == rank)
        .YieldWhenTrue(card => $"{card} is a {rank}")
        .YieldWhenFalse(card => $"{card} is not a {rank}")
        .CreateSpec($"is a {rank}"));