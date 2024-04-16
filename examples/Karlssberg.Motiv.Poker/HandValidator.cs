namespace Karlssberg.Motiv.Poker;

public class HandValidator() : Spec<Hand>(
    Spec.Build((Hand hand) =>
            hand.Cards
                .GroupBy(card => card)
                .Select(group => new CardCollectionHasUniqueItemsPredicate().IsSatisfiedBy(group))
                .OrTogether())
        .WhenTrue("has 5 unique cards")
        .WhenFalse((hand, result) => result.Assertions)
        .Create())
{
    private class CardCollectionHasUniqueItemsPredicate() : Spec<IEnumerable<Card>>(
        Spec.Build((IEnumerable<Card> cards) => cards.Count() == 1)
            .WhenTrue("all the cards are unique")
            .WhenFalse(cards => cards.Select(card => $"{card.Rank} of {card.Suit} is duplicate"))
            .Create("is unqiue")
    );
};