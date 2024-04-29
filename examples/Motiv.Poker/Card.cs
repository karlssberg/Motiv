namespace Motiv.Poker;

public record Card(Suit Suit, Rank Rank)
{
    public Card(string rank, char suit)
        : this(ResolveSuit(suit), ResolveRank(rank))
    {
    }
    public Card(string rank, Suit suit)
        : this(suit, ResolveRank(rank))
    {
    }

    public Card(string cardName)
        : this(ResolveSuit(cardName[^1]), ResolveRank(cardName[..^1]))
    {
    }

    public override string ToString() => $"{ResolveRankDisplayName(Rank)}{Suit.ToString()[0]}";

    public string ResolveRankDisplayName(Rank rank) => rank.GetDescription();

    public static Rank ResolveRank(string rankAbbreviation) =>
        rankAbbreviation.GetEnumFromDescription<Rank>();

    public static Suit ResolveSuit(char code) =>
        code.ToString().GetEnumFromDescription<Suit>();
}