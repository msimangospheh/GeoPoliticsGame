using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Data.Repositories
{
    public interface IGameRepository
    {
        Task CreateAsync(Game game);
        Task<Game?> GetByIdAsync(string id);
        Task<List<Game>> GetByOwnerAsync(string ownerUserId, GameStatus? status = null);
        Task<bool> ReplaceAsync(Game game);
        Task DeleteAsync(string id);
    }
}
