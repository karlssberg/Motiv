using FluentAssertions;
using Motiv.Poker.HandRankProposition;
using static Motiv.Poker.Tests.HandRanks;

namespace Motiv.Poker.Tests;

public class HandTests
{
    [Theory]
    [InlineData("A, A, A, A, 2", false, HandRank.Unknown)]
    [InlineData("K, A, A, 2, 2", false, HandRank.Unknown)]
    [InlineData("K, A, 2, 2, 2", false, HandRank.Unknown)]
    [InlineData("A, A, K, Q, 10", true, HandRank.Pair)]
    [InlineData("A, K, K, Q, 10", true, HandRank.Pair)]
    [InlineData("A, K, Q, Q, 10", true, HandRank.Pair)]
    public void Should_evaluate_a_pair(string handRanks, bool expected, HandRank expectedRank)
    {
        var cards = handRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandPairProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().Be(expected);
        act.Metadata.Should().AllBeEquivalentTo(expectedRank);
    }
    
    [Theory]
    [InlineData("A, A, 9, 2, 2", true, HandRank.TwoPair)]
    [InlineData("K, K, 3, 2, 2", true, HandRank.TwoPair)]
    [InlineData("K, K, A, A, 2", true, HandRank.TwoPair)]
    [InlineData("4, K, A, A, 4", true, HandRank.TwoPair)]
    [InlineData("4, 4, 5, 6, 7", false, HandRank.Unknown)]
    [InlineData("A, A, 8, K, 2", false, HandRank.Unknown)]
    [InlineData("A, A, A, K, 2", false, HandRank.Unknown)]
    public void Should_evaluate_two_pairs(string handRanks, bool expected, HandRank expectedRank)
    {
        var cards = handRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandTwoPairProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().Be(expected);
        act.Metadata.Should().AllBeEquivalentTo(expectedRank);
    }
    
    [Theory]
    [InlineData(AceHighStraightBroadway)]
    [InlineData(KingHighStraight)]
    [InlineData(QueenHighStraight)]
    [InlineData(JackHighStraight)]
    [InlineData(TenHighStraight)]
    [InlineData(NineHighStraight)]
    [InlineData(EightHighStraight)]
    [InlineData(SevenHighStraight)]
    [InlineData(SixHighStraight)]
    [InlineData(FiveHighStraightWheelorBicycle)]
    public void Should_evaluate_a_straight(string straightRanks)
    {
        var cards = straightRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandStraightProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.Metadata.Max().Should().Be(HandRank.Straight);
    }

    [Theory]
    [InlineData("A, K, Q, J, 9")]
    [InlineData("K, Q, J, 10, 8")]
    [InlineData("Q, J, 10, 9, 7")]
    [InlineData("J, 10, 9, 8, 6")]
    [InlineData("10, 9, 8, 7, 5")]
    [InlineData("9, 8, 7, 6, 4")]
    [InlineData("8, 7, 6, 5, 3")]
    [InlineData("A, A, 10, 5, 2")]
    public void Should_not_evaluate_a_straight(string straightRanks)
    {
        var cards = straightRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandStraightProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeFalse();
    }

    [Theory]
    [InlineAutoData("A, K, Q, 10, 4")]
    [InlineAutoData("K, Q, J, 10, 2")]
    [InlineAutoData("Q, J, 10, 9, 3")]
    [InlineAutoData("J, 10, 9, 8, 4")]
    [InlineAutoData("10, 9, 8, 7, 5")]
    [InlineAutoData("9, 8, 7, 6, 4")]
    [InlineAutoData("8, 7, 6, 5, 3")]
    public void Should_evaluate_a_flush(string handRanks, Suit flushSuit)
    {
        var cards = handRanks
            .Split(", ")
            .Select(rank => new Card(rank, flushSuit))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandFlushProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.Metadata.Max().Should().Be(HandRank.Flush);
    }

    [Theory]
    [InlineAutoData(AceHighStraightBroadway)]
    [InlineAutoData(KingHighStraight)]
    [InlineAutoData(QueenHighStraight)]
    [InlineAutoData(JackHighStraight)]
    [InlineAutoData(TenHighStraight)]
    [InlineAutoData(NineHighStraight)]
    [InlineAutoData(EightHighStraight)]
    [InlineAutoData(SevenHighStraight)]
    [InlineAutoData(SixHighStraight)]
    [InlineAutoData(FiveHighStraightWheelorBicycle)]
    public void Should_evaluate_a_straight_flush(string handRanks, Suit flushSuit)
    {
        var hand = new Hand(handRanks
            .Split(", ")
            .Select(rank => new Card(rank, flushSuit))
            .ToList());

        var sut = new IsHandStraightFlushProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.Metadata.Max().Should().Be(HandRank.StraightFlush);
    }

    [Theory]
    [AutoData]
    public void Should_evaluate_a_royal_flush(Suit flushSuit)
    {
        var hand = new Hand(new List<Card>
        {
            new("A", flushSuit),
            new("K", flushSuit),
            new("Q", flushSuit),
            new("J", flushSuit),
            new("10", flushSuit)
        });
        var sut = new IsHandRoyalFlushProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.Metadata.Max().Should().Be(HandRank.RoyalFlush);
    }
    
    [Theory]
    [InlineData("AH, KH, QH, JH, 10H", true, HandRank.RoyalFlush, "is a royal flush hand")]
    [InlineData("KH, QH, JH, 10H, 9H", true, HandRank.StraightFlush, "is a straight flush hand")]
    [InlineData("AH, AD, AC, AS, 3C", true, HandRank.FourOfAKind, "is a four of a kind hand")]
    [InlineData("AH, AD, AC, JH, JS", true, HandRank.FullHouse, "is a full house hand")]
    [InlineData("10H, 8H, 6H, 4H, 2H", true, HandRank.Flush, "is a flush hand")]
    [InlineData("10H, 9C, 8S, 7D, 6H", true, HandRank.Straight, "is a straight hand")]
    [InlineData("AH, AD, AS, 10H, 9D", true, HandRank.ThreeOfAKind, "is a three of a kind hand")]
    [InlineData("AH, AD, QS, 10D, 10H", true, HandRank.TwoPair, "is a two pair hand")]
    [InlineData("AH, AD, 10S, 5C, 2H", true, HandRank.Pair, "is a pair hand")]
    [InlineData("AH, 10D, 8S, 5C, 3D", false, HandRank.HighCard, "!is a royal flush hand",
                                                                                            "!is a straight flush hand",
                                                                                            "!is a four of a kind hand",
                                                                                            "!is a full house hand",
                                                                                            "!is a flush hand",
                                                                                            "!is a straight hand",
                                                                                            "!is a three of a kind hand",
                                                                                            "!is a two pair hand", 
                                                                                            "!is a pair hand")]
    public void Should_evaluate_a_winning_hand(string handRanks, bool expected, HandRank expectedRank, params string[] expectedAssertion)
    {
        var cards = handRanks
            .Split(", ")
            .Select(card => new Card(card[..^1], card[^1]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsWinningHandProposition();

        var act = sut.IsSatisfiedBy(hand);

        act.Justification.Should().NotBeNullOrEmpty();
        act.Satisfied.Should().Be(expected);
        act.Metadata.Max().Should().Be(expectedRank);
        act.Explanation.Underlying.GetAssertions().Should().BeEquivalentTo(expectedAssertion);
        act.SubAssertions.Should().BeEquivalentTo(expectedAssertion);
    }
}