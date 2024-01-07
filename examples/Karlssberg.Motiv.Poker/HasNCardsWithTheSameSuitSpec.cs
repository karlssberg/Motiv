namespace Karlssberg.Motiv.Poker;

public class HasNCardsWithTheSameSuitSpec(int sameSuitCount) : Spec<Hand>(
    Spec.Build<Hand>(hand => hand.Suits.GroupBy(suit => suit).Any(collection => collection.Count() == sameSuitCount))
        .YieldWhenTrue(hand => $"has {sameSuitCount} cards in the {hand.Suits.First()} suit")
        .YieldWhenFalse($"does not have {sameSuitCount} cards with the same suit")
        .CreateSpec($"has {sameSuitCount} cards with the same suit"));
    