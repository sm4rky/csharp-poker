namespace PokerAppBackend.Domain;

public readonly record struct FoldResult(bool IsMatchOver, int Winner);