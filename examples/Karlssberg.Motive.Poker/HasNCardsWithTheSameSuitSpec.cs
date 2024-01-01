namespace Karlssberg.Motive.Poker;

public class HasNCardsWithTheSameSuitSpec(int sameSuitCount) : Spec<Hand>(
    hand => hand.Suits.GroupBy(suit => suit).Any(collection => collection.Count() == sameSuitCount),
    $"Has {sameSuitCount} cards with the same suit",
    $"Does not have {sameSuitCount} cards with the same suit");
    