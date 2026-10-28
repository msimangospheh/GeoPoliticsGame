using System.Text.RegularExpressions;
using GeoPolitics_Game.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoPolitics_Game.Data.Repositories
{
    public class MongoCountryRepository : ICountryRepository
    {
        private readonly IMongoCollection<Country> _countries;

        public MongoCountryRepository(MongoContext context) => _countries = context.Countries;

        public async Task<List<Country>> GetAllAsync(string? search = null)
        {
            var filter = Builders<Country>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Regex.Escape stops users injecting regex patterns (security)
                var pattern = Regex.Escape(search.Trim());
                filter = Builders<Country>.Filter.Regex(c => c.Name, new BsonRegularExpression(pattern, "i"));
            }
            return await _countries.Find(filter).SortBy(c => c.Name).ToListAsync();
        }

        public async Task<Country?> GetByCodeAsync(string code) =>
            await _countries.Find(c => c.Code == code.ToUpperInvariant()).FirstOrDefaultAsync();

        // MongoDB picks random countries for us ($sample) - used for "Nine rivals, drawn by fate"
        public async Task<List<Country>> GetRandomAsync(int count, string excludeCode) =>
            await _countries.Aggregate()
                            .Match(c => c.Code != excludeCode)
                            .Sample(count)
                            .ToListAsync();

        public async Task<long> CountAsync() =>
            await _countries.CountDocumentsAsync(Builders<Country>.Filter.Empty);

        public async Task InsertManyAsync(IEnumerable<Country> countries) =>
            await _countries.InsertManyAsync(countries);
    }
}
