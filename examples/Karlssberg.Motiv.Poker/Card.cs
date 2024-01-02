using static Karlssberg.Motiv.Poker.Rank;
using static Karlssberg.Motiv.Poker.Suit;

namespace Karlssberg.Motiv.Poker;

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
    
    public override string ToString() => $"{ResolveRankDisplayName}{Suit.ToString()[0]}";
    
    public string ResolveRankDisplayName(Rank rank) => rank.GetDescription();

    public static Rank ResolveRank(string rankAbbreviation) => 
        rankAbbreviation.GetEnumFromDescription<Rank>();

    public static Suit ResolveSuit(char code) => 
        code.ToString().GetEnumFromDescription<Suit>();
}