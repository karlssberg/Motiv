namespace Karlssberg.Motive.Poker;

public class IsSuit(Suit suit) : Spec<Card>(
    card => card.Suit == suit,
    $"Is {suit}",
    $"Is Not {suit}");