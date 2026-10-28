namespace GeoPolitics_Game.Models
{
    public class Country
    {
        public string Name { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty; // e.g., "Player" or "NPC_1"
        public int SoldierCount { get; set; }
        public int PopulationTier { get; set; }
    }

    public class GameState
    {
        public string GameId { get; set; } = Guid.NewGuid().ToString();
        public string PlayerName { get; set; } = string.Empty;
        public int CurrentRound { get; set; } = 1;
        public List<Country> Countries { get; set; } = new();
        public List<string> BattleLogs { get; set; } = new();
    }
}
