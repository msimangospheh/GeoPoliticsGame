using GeoPolitics_Game.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GeoPolitics_Game.Data
{
    // The ONE place in the app that knows how to reach MongoDB
    public class MongoContext
    {
        public IMongoDatabase Database { get; }

        public MongoContext(IOptions<MongoSettings> options)
        {
            var settings = options.Value;
            var client = new MongoClient(settings.ConnectionString);
            Database = client.GetDatabase(settings.DatabaseName);
        }

        public IMongoCollection<Country> Countries => Database.GetCollection<Country>("countries");
        public IMongoCollection<Game> Games => Database.GetCollection<Game>("games");
        public IMongoCollection<GameEvent> GameEvents => Database.GetCollection<GameEvent>("gameEvents");

        // Indexes make searches fast. Safe to run on every startup.
        public async Task CreateIndexesAsync()
        {
            // Country search by name
            await Countries.Indexes.CreateOneAsync(new CreateIndexModel<Country>(
                Builders<Country>.IndexKeys.Ascending(c => c.Name)));

            // "My campaigns" list, newest first
            await Games.Indexes.CreateOneAsync(new CreateIndexModel<Game>(
                Builders<Game>.IndexKeys.Ascending(g => g.OwnerUserId).Descending(g => g.UpdatedAt)));

            // Stats: win rate by starting country
            await Games.Indexes.CreateOneAsync(new CreateIndexModel<Game>(
                Builders<Game>.IndexKeys.Ascending(g => g.Status).Ascending(g => g.StartCountryCode)));

            // Events in order within a game (unique so no two events share a number)
            await GameEvents.Indexes.CreateOneAsync(new CreateIndexModel<GameEvent>(
                Builders<GameEvent>.IndexKeys.Ascending(e => e.GameId).Ascending(e => e.Seq),
                new CreateIndexOptions { Unique = true }));
        }
    }
}