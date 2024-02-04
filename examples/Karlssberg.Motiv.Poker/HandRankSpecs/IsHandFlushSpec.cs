namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFlushSpec() : Spec<Hand, HandRank>(
    (new AllOfSuit(Suit.Clubs) | new AllOfSuit(Suit.Hearts) | new AllOfSuit(Suit.Spades) | new AllOfSuit(Suit.Diamonds))
        .YieldWhenTrue(HandRank.Flush)
        .YieldWhenFalse(HandRank.HighCard));
        
public class IsSuit(Suit suit) : Spec<Card>(
    Spec.Build<Card>(card => card.Suit == suit)
        .YieldWhenTrue(card => $"{card} is a {suit}")
        .YieldWhenFalse(card => $"{card} is not a {suit}")
        .CreateSpec($"is a {suit}"));

public class AllOfSuit(Suit suit) : Spec<Hand>(
    new IsSuit(suit)
        .ToNSatisfiedSpec(5)
        .YieldWhenTrue($"a flush of {suit}")
        .YieldWhenFalse($"not a flush of {suit}")
        .ChangeModel<Hand>(hand => hand.Cards));
        
