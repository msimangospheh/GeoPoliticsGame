using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Data.Repositories
{
    public interface IGameEventRepository
    {
        Task AddAsync(GameEvent gameEvent);
        Task AddManyAsync(IEnumerable<GameEvent> gameEvents);
        Task<List<GameEvent>> GetForGameAsync(string gameId, int afterSeq = 0);
        Task DeleteForGameAsync(string gameId);
    }
}
