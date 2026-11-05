using System.Text.RegularExpressions;
using GeoPolitics_Game.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoPolitics_Game.Data.Repositories
{
    // The leaderboard is calculated by MongoDB itself with an aggregation pipeline:
    // finished games are grouped by player, then counted, ranked and limited on the database server.
    public class MongoLeaderboardRepository : ILeaderboardRepository
    {
        private readonly IMongoCollection<BsonDocument> _games;

        public MongoLeaderboardRepository(MongoContext context)
        {
            // Raw documents, so the pipeline can use the stored field names (camelCase)
            _games = context.Database.GetCollection<BsonDocument>("games");
        }

        // Only finished campaigns count (enums are stored as text, e.g. "Won")
        private static BsonDocument FinishedFilter(Difficulty? difficulty)
        {
            var match = new BsonDocument("status", new BsonDocument("$in", new BsonArray { "Won", "Lost" }));
            if (difficulty.HasValue) match["difficulty"] = difficulty.Value.ToString();
            return match;
        }

        // { $cond: [ { $eq: ["$status", "Won"] }, whenWon, otherwise ] }
        private static BsonDocument IfWon(BsonValue whenWon, BsonValue otherwise) =>
            new("$cond", new BsonArray { new BsonDocument("$eq", new BsonArray { "$status", "Won" }), whenWon, otherwise });

        public async Task<List<LeaderboardEntry>> GetTopAsync(Difficulty? difficulty, string? search, int limit)
        {
            var pipeline = new List<BsonDocument>
            {
                new("$match", FinishedFilter(difficulty)),

                // Oldest first, so "$last" below picks each player's most recent name
                new("$sort", new BsonDocument("finishedAt", 1)),

                new("$group", new BsonDocument
                {
                    { "_id", "$ownerUserId" },
                    { "name", new BsonDocument("$last", "$ownerName") },
                    { "played", new BsonDocument("$sum", 1) },
                    { "wins", new BsonDocument("$sum", IfWon(1, 0)) },
                    { "fastestWin", new BsonDocument("$min", IfWon("$round", BsonNull.Value)) },  // $min ignores nulls
                    { "lastPlayed", new BsonDocument("$max", "$finishedAt") }
                }),

                new("$addFields", new BsonDocument("winRate",
                    new BsonDocument("$round", new BsonArray
                    {
                        new BsonDocument("$multiply", new BsonArray { new BsonDocument("$divide", new BsonArray { "$wins", "$played" }), 100 }),
                        0
                    })))
            };

            // Search by commander name (escaped, so typed symbols can't change the query)
            if (!string.IsNullOrWhiteSpace(search))
            {
                pipeline.Add(new("$match", new BsonDocument("name",
                    new BsonRegularExpression(Regex.Escape(search.Trim()), "i"))));
            }

            // Most wins, then best win rate, then fastest conquest, then most recent
            pipeline.Add(new("$sort", new BsonDocument { { "wins", -1 }, { "winRate", -1 }, { "fastestWin", 1 }, { "lastPlayed", -1 } }));
            pipeline.Add(new("$limit", limit));

            var docs = await (await _games.AggregateAsync<BsonDocument>(pipeline)).ToListAsync();

            var rows = docs.Select(d => new LeaderboardEntry
            {
                UserId = d["_id"].IsString ? d["_id"].AsString : "",
                Name = d["name"].IsString ? d["name"].AsString : "Unknown commander",
                Played = d["played"].ToInt32(),
                Wins = d["wins"].ToInt32(),
                WinRate = d["winRate"].ToDouble(),
                FastestWin = d["fastestWin"].IsBsonNull ? null : d["fastestWin"].ToInt32(),
                LastPlayed = d["lastPlayed"] is BsonDateTime last ? last.ToUniversalTime() : null
            }).ToList();

            for (int i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
            return rows;
        }

        public async Task<PlatformStats> GetPlatformStatsAsync(Difficulty? difficulty)
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", FinishedFilter(difficulty)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "finished", new BsonDocument("$sum", 1) },
                    { "victories", new BsonDocument("$sum", IfWon(1, 0)) },
                    { "players", new BsonDocument("$addToSet", "$ownerUserId") },
                    { "fastest", new BsonDocument("$min", IfWon("$round", BsonNull.Value)) }
                })
            };

            var doc = await (await _games.AggregateAsync<BsonDocument>(pipeline)).FirstOrDefaultAsync();
            if (doc == null) return new PlatformStats();

            return new PlatformStats
            {
                FinishedGames = doc["finished"].ToInt32(),
                Victories = doc["victories"].ToInt32(),
                Commanders = doc["players"].AsBsonArray.Count,
                FastestWin = doc["fastest"].IsBsonNull ? null : doc["fastest"].ToInt32()
            };
        }
    }
}
