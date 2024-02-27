namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFlushSpec() : Spec<Hand, HandRank>(
    Spec.Build(IsAFlush)
        .WhenTrue(HandRank.Flush)
        .WhenFalse(HandRank.HighCard)
        .CreateSpec("is a flush hand"))
{
    private static SpecBase<Hand, string> IsAFlush =>
        Enum.GetValues<Suit>()
            .Select(HasFiveCardsWithSameSuit)
            .OrTogether();
    
    private static SpecBase<Hand, string> HasFiveCardsWithSameSuit(Suit suit) =>
        Spec.Build(IsSuit(suit))
            .AsNSatisfied(5)
            .WhenTrue($"a flush of {suit}")
            .WhenFalse($"not a flush of {suit}")
            .CreateSpec($"has 5 {suit} cards")
            .ChangeModelTo<Hand>(hand => hand.Cards);

    private static SpecBase<Card, string> IsSuit(Suit suit) =>
        Spec.Build<Card>(card => card.Suit == suit)
            .WhenTrue(card => $"{card} is a {suit}")
            .WhenFalse(card => $"{card} is not a {suit}")
            .CreateSpec($"is a {suit}");
}

