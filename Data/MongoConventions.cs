using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

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

            // Only our game models (Country, Game, GameEvent...) - NOT the login library's classes
            ConventionRegistry.Register("GeoPoliticsConventions", pack,
                type => type.Namespace == "GeoPolitics_Game.Models");

            // MongoDB.Driver 3 must be told how to store Guids (used for user ids)
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // Already registered by a library - that's fine
            }
        }
    }
}