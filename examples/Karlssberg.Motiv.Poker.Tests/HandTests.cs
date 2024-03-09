using FluentAssertions;
using Karlssberg.Motiv.Poker.HandRankSpecs;
using static Karlssberg.Motiv.Poker.Tests.HandRanks;

namespace Karlssberg.Motiv.Poker.Tests;

public class HandTests
{
    [Theory]
    [InlineAutoData("A, A, A, A, 2", false, HandRank.HighCard)]
    [InlineAutoData("K, A, A, 2, 2", false, HandRank.HighCard)]
    [InlineAutoData("K, A, 2, 2, 2", false, HandRank.HighCard)]
    [InlineAutoData("A, A, K, Q, 10", true, HandRank.Pair)]
    [InlineAutoData("A, K, K, Q, 10", true, HandRank.Pair)]
    [InlineAutoData("A, K, Q, Q, 10", true, HandRank.Pair)]
    public void Should_evaluate_a_pair(string handRanks, bool expected, HandRank expectedRank)
    {
        var cards = handRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandPairSpec();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().AllBeEquivalentTo(expectedRank);
    }
    
    [Theory]
    [InlineAutoData("A, A, 9, 2, 2", true, HandRank.TwoPair)]
    [InlineAutoData("K, K, 3, 2, 2", true, HandRank.TwoPair)]
    [InlineAutoData("K, K, A, A, 2", true, HandRank.TwoPair)]
    [InlineAutoData("4, K, A, A, 4", true, HandRank.TwoPair)]
    [InlineAutoData("4, 4, 5, 6, 7", false, HandRank.HighCard)]
    [InlineAutoData("A, A, 8, K, 2", false, HandRank.HighCard)]
    [InlineAutoData("A, A, A, K, 2", false, HandRank.HighCard)]
    [InlineAutoData("A, A, A, K, 2", false, HandRank.HighCard)]
    public void Should_evaluate_two_pairs(string handRanks, bool expected, HandRank expectedRank)
    {
        var cards = handRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandTwoPairSpec();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().AllBeEquivalentTo(expectedRank);
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
    public void Should_evaluate_a_straight(string straightRanks)
    {
        var cards = straightRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandStraightSpec();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.MetadataTree.Max().Should().Be(HandRank.Straight);
    }

    [Theory]
    [InlineAutoData("A, K, Q, J, 9")]
    [InlineAutoData("K, Q, J, 10, 8")]
    [InlineAutoData("Q, J, 10, 9, 7")]
    [InlineAutoData("J, 10, 9, 8, 6")]
    [InlineAutoData("10, 9, 8, 7, 5")]
    [InlineAutoData("9, 8, 7, 6, 4")]
    [InlineAutoData("8, 7, 6, 5, 3")]
    public void Should_not_evaluate_a_straight(string straightRanks)
    {
        var cards = straightRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new IsHandStraightSpec();

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

        var sut = new IsHandFlushSpec();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.MetadataTree.Max().Should().Be(HandRank.Flush);
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

        var sut = new IsHandStraightFlushSpec();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.MetadataTree.Max().Should().Be(HandRank.StraightFlush);
    }

    [Theory]
    [InlineAutoData]
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
        var sut = new IsHandRoyalFlushSpec();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.Should().BeTrue();
        act.MetadataTree.Max().Should().Be(HandRank.RoyalFlush);
    }
}