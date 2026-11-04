using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoPolitics_Game.Data
{
    // Stores the keys that protect login cookies and form tokens in MongoDB.
    // Free hosting puts the app to sleep and restarts it; without this, every
    // restart would create new keys and log every player out.
    public class MongoXmlRepository : IXmlRepository
    {
        private readonly IMongoCollection<BsonDocument> _keys;

        public MongoXmlRepository(MongoContext context)
        {
            _keys = context.Database.GetCollection<BsonDocument>("dataProtectionKeys");
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            return _keys.Find(FilterDefinition<BsonDocument>.Empty)
                        .ToList()
                        .Select(doc => XElement.Parse(doc["xml"].AsString))
                        .ToList();
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            _keys.InsertOne(new BsonDocument
            {
                { "name", friendlyName },
                { "xml", element.ToString(SaveOptions.DisableFormatting) },
                { "createdAt", DateTime.UtcNow }
            });
        }
    }
}
