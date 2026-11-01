using System.Text.Json;
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

        // GET /Play/{id}?from=ZA&to=IN&replay=true
        // ...but WATCHING is open to anyone with the link (if the owner allowed spectators)
        [AllowAnonymous]
        [HttpGet("/Play/{id}")]
        public async Task<IActionResult> Index(string id, string? from, string? to, bool replay = false)
        {
            var game = await _games.GetByIdAsync(id);
            if (game == null || game.Status == GameStatus.Setup) return NotFound();

            bool isOwner = User.IsOwnerOf(game);
            if (!isOwner && !game.Settings.AllowSpectators) return NotFound();

            var events = await _events.GetForGameAsync(id);
            string? replayJson = null;

            // Right after the owner's turn: rewind the board and let the browser play the NPC moves back
            if (replay && isOwner && game.Replay is { } info)
            {
                var npcMoves = events.Where(e => e.Seq >= info.FromSeq).ToList();
                events = events.Where(e => e.Seq < info.FromSeq).ToList();
                RewindForReplay(game, info);
                replayJson = BuildReplayJson(game, npcMoves);
            }

            var model = new BoardViewModel(game, isOwner, from, to, events, replayJson);
            return View(game.ActiveBattle != null && replayJson == null ? "Battle" : "Board", model);
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

                // Only headline the PLAYER's own result - the replay will show what the NPCs did
                var headline = events.LastOrDefault(e => !e.IsNpc && e.Type is GameEventType.Conquered
                                                         or GameEventType.AttackFailed or GameEventType.Retreated
                                                         or GameEventType.GameWon);
                if (headline != null)
                {
                    // Add the Chronicler's line for the final roll, e.g. "India falls! ..."
                    var lastRoll = events.LastOrDefault(e => e.Type == GameEventType.DiceRolled && !e.IsNpc);
                    var quote = lastRoll?.Commentary is { Length: > 0 } c ? $"  \u201C{c}\u201D" : "";
                    TempData[headline.Type == GameEventType.AttackFailed ? "Error" : "Success"] = headline.Message + quote;
                }

                bool npcMoved = events.Any(e => e.IsNpc);
                return npcMoved
                    ? RedirectToAction(nameof(Index), new { id, replay = true })
                    : RedirectToAction(nameof(Index), new { id });
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

        // Puts the board back to how it looked before the NPCs moved (in memory only - nothing is saved)
        private static void RewindForReplay(Game game, ReplayInfo info)
        {
            foreach (var state in info.Territories)
            {
                var t = game.Territories.FirstOrDefault(x => x.CountryCode == state.CountryCode);
                if (t == null) continue;
                t.OwnerPlayerId = state.OwnerPlayerId;
                t.Soldiers = state.Soldiers;
            }
            game.Round = info.Round;
            game.PlacedThisTurn.Clear();
            game.Status = GameStatus.InProgress;   // don't spoil the ending before the replay shows it
        }

        // The NPC moves, packaged as JSON for wwwroot/js/replay.js
        private static string BuildReplayJson(Game game, List<GameEvent> moves)
        {
            var data = new
            {
                human = game.HumanPlayerId,
                players = game.Players.Select(p => new { id = p.Id, name = p.Name, badge = p.Badge, human = p.IsHuman }),
                events = moves.Select(e => new
                {
                    type = e.Type.ToString(),
                    actor = e.ActorPlayerId,
                    round = e.Round,
                    message = e.Message,
                    reason = e.Reason,
                    from = e.FromCode,
                    to = e.ToCode,
                    amount = e.Amount,
                    placements = e.Placements,
                    aDice = e.AttackerDice,
                    dDice = e.DefenderDice,
                    aLoss = e.AttackerLosses,
                    dLoss = e.DefenderLosses,
                    commentary = e.Commentary
                })
            };
            // The default encoder escapes <, > and & so the JSON is safe to put inside a <script> tag
            return JsonSerializer.Serialize(data);
        }
    }
}