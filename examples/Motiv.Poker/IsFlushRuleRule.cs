using Motiv.Poker.Models;

namespace Motiv.Poker;

public class IsFlushRuleRule() : Policy<Hand, HandRank>(
    Spec.Build(IsAFlush)
        .WhenTrue(HandRank.Flush)
        .WhenFalse(HandRank.HighCard)
        .Create("is a flush hand"))
{
    private static SpecBase<Hand, string> IsAFlush =>
        Enum.GetValues<Suit>()
            .Select(HasFiveCardsWithSameSuit)
            .OrTogether()
            .ChangeModelTo<Hand>(hand => hand.Cards);

    private static SpecBase<IEnumerable<Card>, string> HasFiveCardsWithSameSuit(Suit suit) =>
        Spec.Build(IsSuit(suit))
            .AsNSatisfied(5)
            .WhenTrue($"a flush of {suit}")
            .WhenFalse($"not a flush of {suit}")
            .Create($"has 5 {suit} cards");

    private static SpecBase<Card, string> IsSuit(Suit suit) =>
        Spec.Build((Card card) => card.Suit == suit)
            .WhenTrue(card => $"{card} is {suit}")
            .WhenFalse(card => $"{card} is not {suit}")
            .Create($"is {suit}");
}

