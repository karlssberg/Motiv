
using Motiv.Poker.Models;
using Shouldly;
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

        var sut = new IsPairRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBe(expected);
        act.Value.ShouldBe(expectedRank);
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

        var sut = new IsTwoPairRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBe(expected);
        act.Values.ShouldBe([expectedRank]);
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

        var sut = new IsStraightRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBeTrue();
        act.Values.Max().ShouldBe(HandRank.Straight);
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

        var sut = new IsStraightRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBeFalse();
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

        var sut = new IsFlushRuleRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBeTrue();
        act.Values.Max().ShouldBe(HandRank.Flush);
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

        var sut = new IsStraightFlushRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBeTrue();
        act.Values.Max().ShouldBe(HandRank.StraightFlush);
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
        var sut = new IsRoyalFlushRule();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBeTrue();
        act.Values.Max().ShouldBe(HandRank.RoyalFlush);
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
    [InlineData("AH, 10D, 8S, 5C, 3D", true, HandRank.HighCard, "is a high card hand")]
    public void Should_evaluate_a_playable_hand(string handRanks, bool expected, HandRank expectedRank, params string[] expectedAssertion)
    {
        var cards = handRanks
            .Split(", ")
            .Select(card => new Card(card[..^1], card[^1]))
            .ToList();

        var hand = new Hand(cards);

        var sut = new PokerRules();

        var act = sut.IsSatisfiedBy(hand);

        act.Satisfied.ShouldBe(expected);
        act.Value.ShouldBe(expectedRank);
        act.Justification.ShouldNotBeNullOrEmpty();
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Fact]
    public void Should_demo_winning_hand()
    {
        var sut = new PokerRules();

        var act = sut.IsSatisfiedBy(new Hand("KH, QH, JH, 10H, 9H"));

        act.Justification.ShouldBe(
            """
            OR
                is a straight flush hand
                    AND
                        is a straight hand
                            OR
                                is King High Straight
                                    all cards are King, Queen, Jack, Ten, and Nine
                        is a flush hand
                            OR
                                a flush of Hearts
                                    KH is Hearts
                                        (Card card) => card.Suit == Hearts == true
                                            card.Suit == Suit.Hearts
                                    QH is Hearts
                                        (Card card) => card.Suit == Hearts == true
                                            card.Suit == Suit.Hearts
                                    JH is Hearts
                                        (Card card) => card.Suit == Hearts == true
                                            card.Suit == Suit.Hearts
                                    10H is Hearts
                                        (Card card) => card.Suit == Hearts == true
                                            card.Suit == Suit.Hearts
                                    9H is Hearts
                                        (Card card) => card.Suit == Hearts == true
                                            card.Suit == Suit.Hearts
            """);
    }
}
