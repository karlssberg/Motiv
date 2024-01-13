namespace Karlssberg.Motiv.Poker;

public class  IsSuitSpec(Suit suit) : Spec<Card>(
    Spec.Build<Card>(card => card.Suit == suit)
        .YieldWhenTrue($"Is {suit}")
        .YieldWhenFalse($"Is Not {suit}")
        .CreateSpec());