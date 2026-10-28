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
    }
}
