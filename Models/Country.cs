using MongoDB.Bson.Serialization.Attributes;

namespace GeoPolitics_Game.Models
{
    // One real-world country (stored in the "countries" collection)
    public class Country
    {
        [BsonId]                         // the country code IS the id, e.g. "ZA"
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Population { get; set; }
        public int SoldierValue { get; set; }

        [BsonIgnore]                     // calculated, not saved
        public string FlagUrl => $"https://flagcdn.com/w80/{Code.ToLowerInvariant()}.png";

        [BsonIgnore]                     // "≈ 64.7M people" on the new-game screen
        public string PopulationLabel => Population >= 1_000_000
            ? $"≈ {Population / 1_000_000.0:0.#}M people"
            : $"≈ {Population:N0} people";
    }
}