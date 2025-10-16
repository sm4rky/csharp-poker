namespace PokerAppBackend.Domain;

public sealed class PotManager
{
    private readonly Dictionary<int, int> _commitmentMap = new();

    public void Add(int seat, int amount)
    {
        if (amount <= 0) return;
        _commitmentMap.TryGetValue(seat, out var old);
        _commitmentMap[seat] = old + amount;
    }

    public void ResetAll() => _commitmentMap.Clear();

    public IReadOnlyList<(int total, List<Player> participatingPlayers)> BuildSidePots(IEnumerable<Player> players)
    {
        var activePlayers = players.Where(p => p.CommittedThisHand > 0).ToList();
        if (activePlayers.Count == 0)
            return [];

        var pots = activePlayers
            .Select(p => p.CommittedThisHand)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        var result = new List<(int total, List<Player> participatingPlayers)>();
        var previousCap = 0;

        foreach (var potCap in pots)
        {
            var potTotal = 0;
            var participatingPlayersSet = new HashSet<Player>();

            foreach (var player in activePlayers)
            {
                var contribution = Math.Min(player.CommittedThisHand, potCap) - previousCap;
                if (contribution > 0)
                {
                    potTotal += contribution;
                    participatingPlayersSet.Add(player);
                }
            }

            if (potTotal > 0)
                result.Add((potTotal, participatingPlayersSet.ToList()));

            previousCap = potCap;
        }

        return result;
    }
}