namespace PokerAppBackend.Domain;

public sealed record BotAction(PlayerAction Action, int? RaiseTo = null);