using MongoDB.Bson.Serialization.Attributes;

namespace GeoPolitics_Game.Models
{
    // One row of the leaderboard, produced by a MongoDB aggregation over the "games" collection
    public class LeaderboardEntry
    {
        [BsonId] public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;          // "Commander Sphe"
        public int Played { get; set; }                           // finished campaigns
        public int Wins { get; set; }
        public double WinRate { get; set; }                       // 0-100
        public int? FastestWin { get; set; }                      // fewest rounds to conquer the world
        public DateTime? LastPlayed { get; set; }

        [BsonIgnore] public int Rank { get; set; }
        [BsonIgnore] public string ShortName => Name.StartsWith("Commander ") ? Name["Commander ".Length..] : Name;
    }

    // Totals for the whole platform (all players)
    public class PlatformStats
    {
        public int FinishedGames { get; set; }
        public int Victories { get; set; }
        public int Commanders { get; set; }
        public int? FastestWin { get; set; }
    }
}
