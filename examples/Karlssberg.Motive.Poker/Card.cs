using static Karlssberg.Motive.Poker.Rank;
using static Karlssberg.Motive.Poker.Suit;

namespace Karlssberg.Motive.Poker;

public record Card(Rank Rank, Suit Suit)
{
    public Card(string rank, char suit) 
        : this(ResolveRank(rank), ResolveSuit(suit))
    {
    }
    public Card(string rank, Suit suit) 
        : this(ResolveRank(rank), suit)
    {
    }
    
    public Card(string cardName) 
        : this(ResolveRank(cardName[..^1]), ResolveSuit(cardName[^1]))
    {
    }

    public static Rank ResolveRank(string rankAbbreviation)
    {
        var rank = rankAbbreviation switch
        {
            "2" => Two,
            "3" => Three,
            "4" => Four,
            "5" => Five,
            "6" => Six,
            "7" => Seven,
            "8" => Eight,
            "9" => Nine,
            "10" => Ten,
            "J" => Jack,
            "Q" => Queen,
            "K" => King,
            "A" => Ace,
            _ => throw new ArgumentException($"Invalid rank: {rankAbbreviation}")
        };
        return rank;
    }

    public static Suit ResolveSuit(char code)
    {
        return code switch
        {
            'C' => Clubs,
            'D' => Diamonds,
            'H' => Hearts,
            'S' => Spades,
            _ => throw new ArgumentException($"Invalid suit: {code}")
        };
    }
}