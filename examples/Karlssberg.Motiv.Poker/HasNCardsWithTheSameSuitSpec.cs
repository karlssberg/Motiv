namespace Karlssberg.Motiv.Poker;

public class HasNCardsWithTheSameSuitSpec(int sameSuitCount) : Spec<Hand>(
    $"Has {sameSuitCount} cards with the same suit",
    hand => hand.Suits.GroupBy(suit => suit).Any(collection => collection.Count() == sameSuitCount),
    hand => $"Has {sameSuitCount} cards in the {hand.Suits.First()} suit",
    $"Does not have {sameSuitCount} cards with the same suit");
    