using Motiv.Poker.Models;

namespace Motiv.Poker;

public class IsFlushRuleRule() : Policy<Hand, HandRank>(
    Spec.Build(IsAFlush)
        .WhenTrue(HandRank.Flush)
        .WhenFalse(HandRank.Unknown)
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
        Spec.From((Card card) => card.Suit == Display.AsValue(suit))
            .WhenTrue(card => $"{card} is {suit}")
            .WhenFalse(card => $"{card} is not {suit}")
            .Create($"is {suit}");
}

