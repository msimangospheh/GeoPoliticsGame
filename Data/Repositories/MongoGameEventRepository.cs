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

        // Saves several events at once, numbering them in order
        public async Task AddManyAsync(IEnumerable<GameEvent> gameEvents)
        {
            var list = gameEvents.ToList();
            if (list.Count == 0) return;

            var gameId = list[0].GameId;
            var last = await _events.Find(e => e.GameId == gameId)
                                    .SortByDescending(e => e.Seq)
                                    .Limit(1)
                                    .FirstOrDefaultAsync();
            int seq = last?.Seq ?? 0;
            foreach (var e in list) e.Seq = ++seq;

            await _events.InsertManyAsync(list);
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
