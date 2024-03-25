namespace Karlssberg.Motiv.Poker;

public class DoesHandContainSpecifiedRanksSpec(ICollection<Rank> ranks) : Spec<Hand>(
    Spec.Build(UnderlyingSpec(ranks))
        .AsAllSatisfied()
        .WhenTrue($"all cards are either {string.Join(", or ", ranks)}")
        .WhenFalse(evaluation => evaluation.CausalResults.SelectMany(r => r.ExplanationTree.Assertions))
        .Create()
        .ChangeModelTo<Hand>(hand => hand.Cards))
{
    private static SpecBase<Card, string> UnderlyingSpec(ICollection<Rank> ranks) =>
        Spec.Build((Card card) => ranks.Contains(card.Rank))
            .WhenTrue($"Is one of {string.Join(", ", ranks)}")
            .WhenFalse($"Is not one of {string.Join(", ", ranks)}")
            .Create();
}