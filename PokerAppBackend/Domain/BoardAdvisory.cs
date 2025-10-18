namespace PokerAppBackend.Domain;

public sealed class BoardAdvisory
{
    public Street Street { get; init; }
    public BoardTexture Texture { get; init; }
    public bool Paired { get; init; }
    public bool Monotone { get; init; }
    public int StraightThreatScore { get; init; }
    public int FlushThreatScore { get; init; }
    public bool TripsPossibleOnBoard { get; init; }
}