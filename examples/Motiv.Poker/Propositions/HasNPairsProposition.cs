using Motiv.Poker.Models;

namespace Motiv.Poker.Propositions;

public class HasNPairsProposition(int pairCount) : Policy<Hand>(
    Spec.Build((Hand hand) =>
            hand.Ranks
                .GroupBy(r => r)
                .Count(IsAPair) == pairCount)
        .WhenTrue($"Has {pairCount} {PluralizePair(pairCount)}")
        .WhenFalse($"Does not have {pairCount} {PluralizePair(pairCount)}")
        .Create())
{
    private static bool IsAPair(IEnumerable<Rank> g) => g.Count() == 2;
    private static string PluralizePair(int count) =>
        count == 1 ? "pair" : "pairs";
}

