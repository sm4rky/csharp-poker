using PokerAppBackend.Domain;

public readonly record struct SidePot(int Total, Player[] EligiblePlayers);