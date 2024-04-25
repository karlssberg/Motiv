namespace Karlssberg.Motiv.Poker;

public class DoAllCardsMatchRanksProposition(ICollection<Rank> ranks) : Spec<Hand>(
    Spec.Build((Hand hand) =>
        {
            if (ranks.Count != hand.Cards.Count)
                return false;
            
            var cardRanks = hand.Cards.Select(card => card.Rank).ToList();
            foreach (var rank  in ranks) 
                cardRanks.Remove(rank);
            
            return cardRanks.Count == 0;
        })
        .WhenTrue($"all cards are {ranks.Serialize()}")
        .WhenFalse($"cards are not {ranks.Serialize()}")
        .Create());