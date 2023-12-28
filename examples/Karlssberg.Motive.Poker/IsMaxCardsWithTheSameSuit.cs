namespace Karlssberg.Motive.Poker;

public class IsMaxCardsWithTheSameSuit(int sameSuitCount) : Spec<Hand>(
    hand => hand.Suits.GroupBy(suit => suit).Max(MeasureSize) == sameSuitCount,
    $"Has {sameSuitCount} cards with the same suit",
    $"Does not have {sameSuitCount} cards with the same suit")
{
    private static int MeasureSize(IEnumerable<Suit> collection) => collection.Count();
}
    