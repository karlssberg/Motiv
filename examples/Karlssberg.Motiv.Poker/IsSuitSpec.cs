namespace Karlssberg.Motiv.Poker;

public class IsSuitSpec(Suit suit) : Spec<Card>(
    card => card.Suit == suit,
    $"Is {suit}",
    $"Is Not {suit}");