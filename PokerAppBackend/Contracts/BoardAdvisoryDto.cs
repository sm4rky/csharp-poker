namespace PokerAppBackend.Contracts;

public sealed class BoardAdvisoryDto
{
    public string Street { get; init; } = "";
    public string Texture { get; init; } = "";
    public bool Paired { get; init; }
    public bool Monotone { get; init; }
    public int StraightThreats { get; init; }
    public int FlushThreats { get; init; }
    public bool TripsPossibleOnBoard { get; init; }
}