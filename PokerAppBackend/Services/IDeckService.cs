using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public interface IDeckService
{
    Guid StartNewDeck();
    void Shuffle(Guid sessionId);
    Card Burn(Guid sessionId);
    Card Draw(Guid sessionId);
    IReadOnlyList<Card> DrawMany(Guid sessionId, int count);
    int Remaining(Guid sessionId);
}