namespace PokerAppBackend.Domain;

public readonly record struct Card(Rank Rank, Suit Suit)
{
    public override string ToString()
    {
        var suitSymbol = Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            Suit.Spades => "♠",
            _ => "?"
        };

        var rankText = Rank switch
        {
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)Rank).ToString()
        };

        return $"{rankText}{suitSymbol}";
    }
}