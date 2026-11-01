using GeoPolitics_Game.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoPolitics_Game.Data.Repositories
{
    public class MongoGameRepository : IGameRepository
    {
        private readonly IMongoCollection<Game> _games;

        public MongoGameRepository(MongoContext context) => _games = context.Games;

        public async Task CreateAsync(Game game) => await _games.InsertOneAsync(game);

        public async Task<Game?> GetByIdAsync(string id)
        {
            // A bad id in the URL (e.g. /Play/hello) returns null instead of crashing
            if (!ObjectId.TryParse(id, out _)) return null;
            return await _games.Find(g => g.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Game>> GetByOwnerAsync(string ownerUserId, GameStatus? status = null)
        {
            var filter = Builders<Game>.Filter.Eq(g => g.OwnerUserId, ownerUserId);
            if (status.HasValue)
                filter &= Builders<Game>.Filter.Eq(g => g.Status, status.Value);

            return await _games.Find(filter).SortByDescending(g => g.UpdatedAt).ToListAsync();
        }

        public async Task<bool> ReplaceAsync(Game game)
        {
            game.UpdatedAt = DateTime.UtcNow;
            var result = await _games.ReplaceOneAsync(g => g.Id == game.Id, game);
            return result.MatchedCount == 1;
        }

        public async Task DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out _)) return;
            await _games.DeleteOneAsync(g => g.Id == id);
        }

        // Public games in progress, most recently played first (for "Live now")
        public async Task<List<Game>> GetLiveAsync(int limit) =>
            await _games.Find(g => g.Status == GameStatus.InProgress && g.Settings.AllowSpectators)
                        .SortByDescending(g => g.UpdatedAt)
                        .Limit(limit)
                        .ToListAsync();
    }
}
