using System.Text.Json;
using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Data
{
    // Runs at startup: creates indexes and loads countries if the collection is empty
    public class DatabaseSeeder
    {
        private readonly MongoContext _context;
        private readonly ICountryRepository _countries;
        private readonly ILogger<DatabaseSeeder> _logger;

        public DatabaseSeeder(MongoContext context, ICountryRepository countries, ILogger<DatabaseSeeder> logger)
        {
            _context = context;
            _countries = countries;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            await _context.CreateIndexesAsync();

            if (await _countries.CountAsync() > 0)
            {
                _logger.LogInformation("Countries already seeded - skipping.");
                return;
            }

            var path = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "countries.json");
            var json = await File.ReadAllTextAsync(path);
            var rows = JsonSerializer.Deserialize<List<CountrySeedRow>>(json,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            var countries = rows.Select(r => new Country
            {
                Code = r.Code.ToUpperInvariant(),
                Name = r.Name,
                Population = r.Population,
                SoldierValue = SoldierTable.ForPopulation(r.Population) // calculated by OUR rule
            }).ToList();

            await _countries.InsertManyAsync(countries);
            _logger.LogInformation("Seeded {Count} countries.", countries.Count);
        }

        // Shape of one row in countries.json
        private class CountrySeedRow
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public long Population { get; set; }
        }
    }
}
