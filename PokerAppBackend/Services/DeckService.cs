using System.Collections.Concurrent;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public sealed class DeckService : IDeckService
{
    private readonly ConcurrentDictionary<Guid, Deck> _sessions = new();

    public Guid StartNewDeck()
    {
        var id = Guid.NewGuid();
        var deck = Deck.Standard();
        _sessions[id] = deck;
        return id;
    }
    
    private Deck Get(Guid id)
    {
        // if (_sessions.TryGetValue(id, out var deck)) return deck;
        // throw new KeyNotFoundException("Session not found. Start a new deck first.");
        
        return _sessions.TryGetValue(id, out var deck)
            ? deck
            : throw new KeyNotFoundException("Session not found. Start a new deck first.");
    }
    
    public void Shuffle(Guid sessionId)
    {
        // var deck = _sessions[sessionId];
        // deck.Shuffle();
        Get(sessionId).Shuffle();
    }

    public Card Burn(Guid sessionId)
    {
        // var deck = _sessions[sessionId];
        // return deck.Burn();
        return Get(sessionId).Burn();
    }

    public Card Draw(Guid sessionId)
    {
        return Get(sessionId).Draw();
    }

    public IReadOnlyList<Card> DrawMany(Guid sessionId, int count)
    {
        return Get(sessionId).DrawMany(count).ToList();
    }

    public int Remaining(Guid sessionId)
    {
        return Get(sessionId).Count;
    }
}