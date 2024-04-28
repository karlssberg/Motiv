namespace Karlssberg.Motiv.Poker;

public class HandValidator() : Spec<Hand>(
    Spec.Build((Hand hand) => HandHasUniqueCards(hand))
        .WhenTrue("has 5 unique cards")
        .WhenFalse((_, result) => result.Assertions)
        .Create())
{
    private static BooleanResultBase<string> HandHasUniqueCards(Hand hand)
    {
        var isUniqueCardResults = hand.Cards
            .GroupBy(card => card)
            .Select(cardGroups => IsUniqueCard.IsSatisfiedBy(cardGroups));
        
        return isUniqueCardResults.OrTogether();
    }

    private static SpecBase<IEnumerable<Card>, string> IsUniqueCard { get; } =
        Spec.Build((IEnumerable<Card> cards) => cards.Count() == 1)
            .WhenTrue(cards => $"{cards.First()} is unique")
            .WhenFalse(cards => cards.Select(card => $"{card.Rank} of {card.Suit} is duplicate"))
            .Create("is unique");
}