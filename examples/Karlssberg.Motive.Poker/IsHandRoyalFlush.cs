namespace Karlssberg.Motive.Poker;

public class IsHandRoyalFlush() : Spec<Hand, HandRank>(
    "Is A Royal Flush Hand",
    hand => hand.Ranks.Distinct().Count() == 5 
            && hand.Ranks.Max() == Rank.Ace 
            && hand.Ranks.Min() == Rank.Ten,
    HandRank.RoyalFlush,
    HandRank.HighCard);