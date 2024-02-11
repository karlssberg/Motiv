namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFlushSpec() : Spec<Hand, HandRank>(
    Spec.Build(Enum.GetValues<Suit>()
            .Select(suit => new HasFiveCardsWithSameSuit(suit))
            .OrTogether())
        .YieldWhenTrue(HandRank.Flush)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a flush hand"));
        

public class HasFiveCardsWithSameSuit(Suit suit) : Spec<Hand>(
    new IsSuit(suit)
        .CreateExactlySpec(5)
        .YieldWhenTrue($"a flush of {suit}")
        .YieldWhenFalse($"not a flush of {suit}")
        .ChangeModel<Hand>(hand => hand.Cards));
        

public class IsSuit(Suit suit) : Spec<Card>(
    Spec.Build<Card>(card => card.Suit == suit)
        .YieldWhenTrue(card => $"{card} is a {suit}")
        .YieldWhenFalse(card => $"{card} is not a {suit}")
        .CreateSpec($"is a {suit}"));