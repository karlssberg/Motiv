namespace Karlssberg.Motiv.Poker;

public class DoesHandContainSpecifiedRanksProposition(ICollection<Rank> ranks) : Spec<Hand>(
    Spec.Build(ContainsRanksProposition(ranks))
        .AsAllSatisfied()
        .WhenTrue($"all cards are either {ranks.Serialize("or")}")
        .WhenFalse(evaluation => evaluation.CausalResults.SelectMany(r => r.Explanation.Assertions))
        .Create()
        .ChangeModelTo<Hand>(hand => hand.Cards))
{
    private static SpecBase<Card, string> ContainsRanksProposition(ICollection<Rank> ranks) =>
        Spec.Build((Card card) => ranks.Contains(card.Rank))
            .WhenTrue($"Is one of {ranks.Serialize("or")}")
            .WhenFalse($"Is not one of {ranks.Serialize("or")}")
            .Create();
}