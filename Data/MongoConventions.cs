using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace GeoPolitics_Game.Data
{
    // Rules for how our C# classes are stored in MongoDB
    public static class MongoConventions
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),              // CountryCode -> countryCode
                new IgnoreExtraElementsConvention(true),           // old fields won't crash the app
                new EnumRepresentationConvention(BsonType.String)  // enums saved as "InProgress", not 0
            };

            // Only apply to OUR classes (not the login library we add later)
            ConventionRegistry.Register("GeoPoliticsConventions", pack,
                type => type.Namespace?.StartsWith("GeoPolitics_Game") == true);
        }
    }
}
