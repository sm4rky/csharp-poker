namespace PokerAppBackend.Domain;

public sealed class BoardAdvisory
{
    public Street Street { get; init; }
    public BoardTexture Texture { get; init; }
    public bool Paired { get; init; }
    public bool Monotone { get; init; }
    public int StraightThreats { get; init; }
    public int FlushThreats { get; init; }
    public bool TripsPossibleOnBoard { get; init; }
}