using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Data.Repositories
{
    public interface ICountryRepository
    {
        Task<List<Country>> GetAllAsync(string? search = null);
        Task<Country?> GetByCodeAsync(string code);
        Task<List<Country>> GetRandomAsync(int count, string excludeCode);
        Task<long> CountAsync();
        Task InsertManyAsync(IEnumerable<Country> countries);
    }
}
