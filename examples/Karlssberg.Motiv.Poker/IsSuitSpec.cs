namespace Karlssberg.Motiv.Poker;

public class IsSuitSpec(Suit suit) : Spec<Card>(
    Spec.Build<Card>(card => card.Suit == suit)
        .WhenTrue($"Is {suit}")
        .WhenFalse($"Is Not {suit}")
        .CreateSpec());