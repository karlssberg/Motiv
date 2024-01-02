using FluentAssertions;
using Karlssberg.Motiv.Poker.HandRankSpecs;
using Karlssberg.Motiv.Poker.StraightHands;
using static Karlssberg.Motiv.Poker.Tests.HandRanks;

namespace Karlssberg.Motiv.Poker.Tests;

public class HandTests
{
    [Theory]
    [AutoParams(AceHighStraightBroadway)]
    [AutoParams(KingHighStraight)]
    [AutoParams(QueenHighStraight)]
    [AutoParams(JackHighStraight)]
    [AutoParams(TenHighStraight)]
    [AutoParams(NineHighStraight)]
    [AutoParams(EightHighStraight)]
    [AutoParams(SevenHighStraight)]
    [AutoParams(SixHighStraight)]
    [AutoParams(FiveHighStraightWheelorBicycle)]
    public void Should_evaluate_a_straight(string straightRanks)
    {
        var cards = straightRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();
        
        var hand = new Hand(cards);
        
        var sut = new IsHandStraightSpec();
        
        var act = sut.IsSatisfiedBy(hand);
            
        act.IsSatisfied.Should().BeTrue();
        act.GetInsights().Max().Should().Be(HandRank.Straight);
    }
    
    [Theory]
    [AutoParams("A, K, Q, J, 9")]
    [AutoParams("K, Q, J, 10, 8")]
    [AutoParams("Q, J, 10, 9, 7")]
    [AutoParams("J, 10, 9, 8, 6")]
    [AutoParams("10, 9, 8, 7, 5")]
    [AutoParams("9, 8, 7, 6, 4")]
    [AutoParams("8, 7, 6, 5, 3")]  
    public void Should_not_evaluate_a_straight(string straightRanks)
    {
        var cards = straightRanks
            .Split(", ")
            .Select(rank => new Card(rank, Suit.Clubs.ToString()[0]))
            .ToList();
        
        var hand = new Hand(cards);
        
        var sut = new IsHandStraightSpec();
        
        var act = sut.IsSatisfiedBy(hand);

        act.IsSatisfied.Should().BeFalse();
    }
    
    [Theory]
    [AutoParams("A, K, Q, 10, 4")]
    [AutoParams("K, Q, J, 10, 2")]
    [AutoParams("Q, J, 10, 9, 3")]
    [AutoParams("J, 10, 9, 8, 4")]
    [AutoParams("10, 9, 8, 7, 5")]
    [AutoParams("9, 8, 7, 6, 4")]
    [AutoParams("8, 7, 6, 5, 3")]
    public void Should_evaluate_a_flush(string handRanks, Suit flushSuit)
    {
        var cards = handRanks
            .Split(", ")
            .Select(rank => new Card(rank, flushSuit))
            .ToList();
        
        var hand = new Hand(cards); 
        
        var sut = new IsHandFlushSpec();
        
        var act = sut.IsSatisfiedBy(hand);
            
        act.IsSatisfied.Should().BeTrue();
        act.GetInsights().Max().Should().Be(HandRank.Flush);
    }
    
    [Theory]
    [AutoParams(AceHighStraightBroadway)]
    [AutoParams(KingHighStraight)]
    [AutoParams(QueenHighStraight)]
    [AutoParams(JackHighStraight)] 
    [AutoParams(TenHighStraight)]
    [AutoParams(NineHighStraight)]
    [AutoParams(EightHighStraight)]
    [AutoParams(SevenHighStraight)]
    [AutoParams(SixHighStraight)]
    [AutoParams(FiveHighStraightWheelorBicycle)]
    public void Should_evaluate_a_straight_flush(string handRanks, Suit flushSuit)
    {
        var hand = new Hand(handRanks
            .Split(", ")
            .Select(rank => new Card(rank, flushSuit))
            .ToList()); 
        
        var sut = new IsHandStraightFlushSpec();
        
        var act = sut.IsSatisfiedBy(hand);
        
        act.IsSatisfied.Should().BeTrue();
        act.GetInsights().Max().Should().Be(HandRank.StraightFlush);
    }
    
    [Theory]
    [AutoParams]
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
        
        act.IsSatisfied.Should().BeTrue();
        act.GetInsights().Max().Should().Be(HandRank.RoyalFlush);
    }
}