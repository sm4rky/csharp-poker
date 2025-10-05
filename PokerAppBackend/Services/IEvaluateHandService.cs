using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public interface IEvaluateHandService
{
    HandValue EvaluateHand(IReadOnlyList<Card> hole, IReadOnlyList<Card> community);
}