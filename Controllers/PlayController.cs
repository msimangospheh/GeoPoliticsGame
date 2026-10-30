using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.Security;
using GeoPolitics_Game.Services;
using GeoPolitics_Game.Services.Engine;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    [Authorize]   // every MOVE needs a logged-in owner...
    public class PlayController : Controller
    {
        private readonly IGameRepository _games;
        private readonly IGameEventRepository _events;
        private readonly IGameService _service;
        private readonly IGameEngine _engine;

        public PlayController(IGameRepository games, IGameEventRepository events, IGameService service, IGameEngine engine)
        {
            _games = games;
            _events = events;
            _service = service;
            _engine = engine;
        }

        // GET /Play/{id}?from=ZA&to=IN
        // ...but WATCHING is open to anyone with the link (if the owner allowed spectators)
        [AllowAnonymous]
        [HttpGet("/Play/{id}")]
        public async Task<IActionResult> Index(string id, string? from, string? to)
        {
            var game = await _games.GetByIdAsync(id);
            if (game == null || game.Status == GameStatus.Setup) return NotFound();

            bool isOwner = User.IsOwnerOf(game);
            if (!isOwner && !game.Settings.AllowSpectators) return NotFound();

            var events = await _events.GetForGameAsync(id);
            var model = new BoardViewModel(game, isOwner, from, to, events);

            // An attack in progress shows the battle screen; otherwise the board
            return View(game.ActiveBattle != null ? "Battle" : "Board", model);
        }

        // GET /Play/{id}/Ledger?show=attack   - every NPC decision with its reason (public, like the board)
        [AllowAnonymous]
        [HttpGet("/Play/{id}/Ledger")]
        public async Task<IActionResult> Ledger(string id, string? show)
        {
            var game = await _games.GetByIdAsync(id);
            if (game == null || game.Status == GameStatus.Setup) return NotFound();
            if (!User.IsOwnerOf(game) && !game.Settings.AllowSpectators) return NotFound();

            var entries = (await _events.GetForGameAsync(id))
                .Where(e => e.IsNpc && !string.IsNullOrEmpty(e.Reason))
                .Where(e => string.IsNullOrEmpty(show) || e.Category == show)
                .Reverse()
                .Take(150)
                .ToList();

            return View(new LedgerViewModel { Game = game, Entries = entries, Show = show });
        }

        [HttpPost("/Play/{id}/Reinforce")]
        public Task<IActionResult> Reinforce(string id, Dictionary<string, int> placements) =>
            Act(id, game => _engine.PlaceReinforcements(game, game.HumanPlayerId, placements));

        [HttpPost("/Play/{id}/Attack")]
        public Task<IActionResult> Attack(string id, string from, string to) =>
            Act(id, game => _engine.StartAttack(game, game.HumanPlayerId, from, to));

        [HttpPost("/Play/{id}/Roll")]
        public Task<IActionResult> Roll(string id) =>
            Act(id, game => _engine.Roll(game, game.HumanPlayerId));

        [HttpPost("/Play/{id}/Retreat")]
        public Task<IActionResult> Retreat(string id) =>
            Act(id, game => _engine.Retreat(game, game.HumanPlayerId));

        [HttpPost("/Play/{id}/Transfer")]
        public Task<IActionResult> Transfer(string id, string from, string to, int amount) =>
            Act(id, game => _engine.Transfer(game, game.HumanPlayerId, from, to, amount));

        [HttpPost("/Play/{id}/EndTurn")]
        public Task<IActionResult> EndTurn(string id) =>
            Act(id, game => _engine.EndTurn(game, game.HumanPlayerId));

        // Runs a move through the service (which checks ownership), shows the headline, reloads the board
        private async Task<IActionResult> Act(string id, Func<Game, List<GameEvent>> action)
        {
            try
            {
                var events = await _service.PlayAsync(id, User.GetUserId(), action);

                var headline = events.LastOrDefault(e => e.Type is GameEventType.Conquered or GameEventType.AttackFailed
                                                         or GameEventType.Retreated or GameEventType.GameWon
                                                         or GameEventType.GameLost);
                if (headline != null)
                    TempData[headline.Type is GameEventType.AttackFailed or GameEventType.GameLost ? "Error" : "Success"]
                        = headline.Message;

                return RedirectToAction(nameof(Index), new { id });
            }
            catch (GameRuleException ex)
            {
                TempData["Error"] = ex.Message;   // e.g. "It is not your turn."
                return RedirectToAction(nameof(Index), new { id });
            }
            catch (Exception ex) when (ex is KeyNotFoundException or UnauthorizedAccessException)
            {
                return NotFound();   // not your game: don't reveal anything
            }
        }
    }
}