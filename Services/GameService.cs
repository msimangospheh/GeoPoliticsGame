using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services.Engine;
using GeoPolitics_Game.Services.Npc;

namespace GeoPolitics_Game.Services
{
    // The "application layer": loads games, checks ownership, runs the engine, saves results.
    // Controllers call this; they never touch the engine or database directly.
    public interface IGameService
    {
        Task<Game> CreateDraftAsync(string ownerUserId, string ownerName, string homelandCode);
        Task<Game> RedrawRivalsAsync(string gameId, string userId);
        Task<Game> LaunchAsync(string gameId, string userId, Difficulty difficulty, GameSettings settings);
        Task<Game> GetOwnedAsync(string gameId, string userId);
        Task DeleteAsync(string gameId, string userId);
    }

    public class GameService : IGameService
    {
        private const int RivalCount = 9;

        private readonly IGameRepository _games;
        private readonly IGameEventRepository _events;
        private readonly ICountryRepository _countries;
        private readonly IGameEngine _engine;
        private readonly INpcPlayer _npc;

        public GameService(IGameRepository games, IGameEventRepository events, ICountryRepository countries,
                           IGameEngine engine, INpcPlayer npc)
        {
            _games = games;
            _events = events;
            _countries = countries;
            _engine = engine;
            _npc = npc;
        }

        // Step 1 -> 2: pick a homeland, the server draws 9 random rivals
        public async Task<Game> CreateDraftAsync(string ownerUserId, string ownerName, string homelandCode)
        {
            var homeland = await _countries.GetByCodeAsync(homelandCode ?? "")
                           ?? throw new GameRuleException("Choose a country from the list.");
            var rivals = await _countries.GetRandomAsync(RivalCount, homeland.Code);

            var game = _engine.CreateDraft(ownerUserId, ownerName, homeland, rivals,
                                           Difficulty.Commander, new GameSettings());
            await _games.CreateAsync(game);
            return game;
        }

        // "Redraw rivals"
        public async Task<Game> RedrawRivalsAsync(string gameId, string userId)
        {
            var game = await GetOwnedAsync(gameId, userId);
            var rivals = await _countries.GetRandomAsync(RivalCount, game.StartCountryCode);
            _engine.ReplaceRivals(game, rivals);
            await _games.ReplaceAsync(game);
            return game;
        }

        // "Launch campaign": roll turn order, then let NPCs play until it's the human's turn
        public async Task<Game> LaunchAsync(string gameId, string userId, Difficulty difficulty, GameSettings settings)
        {
            var game = await GetOwnedAsync(gameId, userId);
            game.Difficulty = difficulty;
            game.Settings = settings;

            var events = _engine.Launch(game);
            events.AddRange(PlayNpcTurns(game));

            await SaveAsync(game, events);
            return game;
        }

        // ACCESS CONTROL: only the owner gets the game back
        public async Task<Game> GetOwnedAsync(string gameId, string userId)
        {
            var game = await _games.GetByIdAsync(gameId)
                       ?? throw new KeyNotFoundException("Campaign not found.");
            if (string.IsNullOrEmpty(userId) || game.OwnerUserId != userId)
                throw new UnauthorizedAccessException("Only the commander who owns this campaign can do that.");
            return game;
        }

        public async Task DeleteAsync(string gameId, string userId)
        {
            var game = await GetOwnedAsync(gameId, userId);
            await _events.DeleteForGameAsync(game.Id);
            await _games.DeleteAsync(game.Id);
        }

        // NPCs take their turns until it's the human's turn again (or the game ends)
        private List<GameEvent> PlayNpcTurns(Game game)
        {
            var events = new List<GameEvent>();
            int safety = 0;   // never loop forever, even if something goes wrong
            while (game.Status == GameStatus.InProgress
                   && game.CurrentPlayerId != game.HumanPlayerId
                   && safety++ < 200)
            {
                events.AddRange(_npc.PlayTurn(game));
            }
            return events;
        }

        private async Task SaveAsync(Game game, List<GameEvent> events)
        {
            await _events.AddManyAsync(events);
            await _games.ReplaceAsync(game);
        }
    }
}