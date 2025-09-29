using System.Security.Cryptography;

namespace PokerAppBackend.Domain;

public sealed class Deck
{
    private readonly List<Card> _cards;
    public IReadOnlyList<Card> Cards => _cards;
    public int Count => _cards.Count;
    public bool IsEmpty => _cards.Count == 0;

    private Deck(List<Card> cards)
    {
        _cards = cards;
    }

    public static Deck Standard()
    {
        // Method 1:
        // var cards = new List<Card>(capacity: 52);
        //
        // for (var r = Rank.Two; r <= Rank.Ace; r++)
        // {
        //     foreach (Suit s in Enum.GetValues(typeof(Suit)))
        //     {
        //         cards.Add(new Card(r, s));
        //     }
        // }
        //
        // return new Deck(cards);

        //Method 2:
        // var cards = (
        //     from Suit s in Enum.GetValues(typeof(Suit))
        //     from Rank r in Enum.GetValues(typeof(Rank))
        //     select new Card(r, s)
        // ).ToList();
        //
        // return new Deck(cards);

        //Method 3:
        var suits = Enum.GetValues(typeof(Suit)).Cast<Suit>();
        var ranks = Enum.GetValues(typeof(Rank)).Cast<Rank>();

        var cards = ranks
            .SelectMany(r => suits.Select(s => new Card(r, s)))
            .ToList();

        return new Deck(cards);
    }

    public void Shuffle()
    {
        for (var i = Count - 1; i > 0; i--)
        {
            var j = RandomInt(0, i + 1); //[0, i]
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    private static int RandomInt(int inclusiveMin, int exclusiveMax)
    {
        return RandomNumberGenerator.GetInt32(inclusiveMin, exclusiveMax); //random in [fromInclusive, toExclusive)
    }

    public Card Draw()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Deck is empty.");

        var top = _cards[^1]; //similar to _card[Count - 1]
        _cards.RemoveAt(Count - 1);
        return top;
    }

    public IEnumerable<Card> DrawMany(int count)
    {
        ArgumentOutOfRangeException
            .ThrowIfNegative(count); // if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        for (var i = 0; i < count; i++)
        {
            yield return Draw();
        }
    }

    public Card Burn()
    {
        return Draw();
    }
}