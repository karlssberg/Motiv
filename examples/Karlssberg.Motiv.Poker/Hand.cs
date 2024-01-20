using static System.StringSplitOptions;

namespace Karlssberg.Motiv.Poker;

public record Hand
{
    private static readonly char[] ParseSeparators = [' ', ','];

    public Hand(ICollection<Card> cards)
    {
        if (cards.Count != 5) throw new ArgumentException("A hand must contain 5 cards");

        Cards = cards;
    }

    public Hand(string handPattern)
        : this(ParseCards(handPattern))
    {
    }

    public IEnumerable<Rank> Ranks => Cards.Select(c => c.Rank);

    public IEnumerable<Suit> Suits => Cards.Select(c => c.Suit);

    public ICollection<Card> Cards { get; init; }

    public static Hand Parse(string hand)
    {
        var cards = ParseCards(hand);

        return new Hand(cards);
    }

    private static List<Card> ParseCards(string hand) =>
        hand
            .Split(ParseSeparators, RemoveEmptyEntries | TrimEntries)
            .Select(code => new Card(code))
            .ToList();

    public override string ToString() => string.Join(' ', Cards);
}