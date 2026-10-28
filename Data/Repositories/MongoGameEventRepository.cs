using GeoPolitics_Game.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoPolitics_Game.Data.Repositories
{
    public class MongoGameEventRepository : IGameEventRepository
    {
        private readonly IMongoCollection<GameEvent> _events;

        public MongoGameEventRepository(MongoContext context) => _events = context.GameEvents;

        public async Task AddAsync(GameEvent gameEvent)
        {
            // Give the event the next number in this game's sequence
            var last = await _events.Find(e => e.GameId == gameEvent.GameId)
                                    .SortByDescending(e => e.Seq)
                                    .Limit(1)
                                    .FirstOrDefaultAsync();
            gameEvent.Seq = (last?.Seq ?? 0) + 1;
            await _events.InsertOneAsync(gameEvent);
        }

        // afterSeq lets spectators fetch only the NEW events since they last checked
        public async Task<List<GameEvent>> GetForGameAsync(string gameId, int afterSeq = 0)
        {
            if (!ObjectId.TryParse(gameId, out _)) return new List<GameEvent>();
            return await _events.Find(e => e.GameId == gameId && e.Seq > afterSeq)
                                .SortBy(e => e.Seq)
                                .ToListAsync();
        }

        public async Task DeleteForGameAsync(string gameId)
        {
            if (!ObjectId.TryParse(gameId, out _)) return;
            await _events.DeleteManyAsync(e => e.GameId == gameId);
        }
    }
}
