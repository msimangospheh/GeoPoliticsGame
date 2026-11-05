using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Data.Repositories
{
    public interface ILeaderboardRepository
    {
        // Best commanders, optionally only games of one difficulty and/or names containing "search"
        Task<List<LeaderboardEntry>> GetTopAsync(Difficulty? difficulty, string? search, int limit);

        // Totals across every finished campaign on the platform
        Task<PlatformStats> GetPlatformStatsAsync(Difficulty? difficulty);
    }
}
