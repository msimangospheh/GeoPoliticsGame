using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.Security;
using GeoPolitics_Game.Services;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    [Authorize]   // every page here needs a logged-in commander
    public class GamesController : Controller
    {
        private readonly IGameRepository _games;
        private readonly ICountryRepository _countries;
        private readonly IGameService _service;

        public GamesController(IGameRepository games, ICountryRepository countries, IGameService service)
        {
            _games = games;
            _countries = countries;
            _service = service;
        }

        // GET /Games?status=InProgress&search=egypt   ("The War Room")
        public async Task<IActionResult> Index(GameStatus? status, string? search)
        {
            var all = await _games.GetByOwnerAsync(User.GetUserId());   // only MY games
            IEnumerable<Game> shown = all;

            if (status.HasValue)
                shown = shown.Where(g => g.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().TrimStart('#');
                shown = shown.Where(g =>
                    g.ShortCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    g.Territories.Any(t => t.CountryName.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return View(new LobbyViewModel { AllGames = all, Shown = shown.ToList(), Status = status, Search = search });
        }

        // GET /Games/New?search=south&code=ZA   (step 1: choose your homeland)
        public async Task<IActionResult> New(string? search, string? code)
        {
            var model = new NewCampaignViewModel
            {
                Search = search,
                Countries = await _countries.GetAllAsync(search)
            };
            if (!string.IsNullOrWhiteSpace(code))
                model.Homeland = await _countries.GetByCodeAsync(code);
            return View(model);
        }

        // POST /Games/Draft   (step 2: the server draws nine rivals)
        [HttpPost]
        public async Task<IActionResult> Draft(string code)
        {
            try
            {
                var game = await _service.CreateDraftAsync(User.GetUserId(), $"Commander {User.GetDisplayName()}", code);
                return RedirectToAction(nameof(Setup), new { id = game.Id });
            }
            catch (GameRuleException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(New));
            }
        }

        // GET /Games/Setup/{id}   (step 3: rules & launch)
        public async Task<IActionResult> Setup(string id)
        {
            var game = await _games.GetByIdAsync(id);
            if (game == null || !User.IsOwnerOf(game)) return NotFound();
            if (game.Status != GameStatus.Setup) return RedirectToAction(nameof(Index));
            return View(new SetupViewModel { Game = game });
        }

        // POST /Games/Redraw/{id}
        [HttpPost]
        public async Task<IActionResult> Redraw(string id)
        {
            try
            {
                await _service.RedrawRivalsAsync(id, User.GetUserId());
                return RedirectToAction(nameof(Setup), new { id });
            }
            catch (Exception ex) when (IsGameError(ex)) { return HandleError(ex, id); }
        }

        // POST /Games/Launch/{id}
        [HttpPost]
        public async Task<IActionResult> Launch(string id, LaunchForm form)
        {
            // Never trust the browser: re-check every value on the server
            if (!ModelState.IsValid || !Enum.IsDefined(form.Difficulty)
                || !ChroniclerVoices.All.Contains(form.CommentatorVoice))
            {
                TempData["Error"] = "Choose a difficulty and a chronicler voice.";
                return RedirectToAction(nameof(Setup), new { id });
            }

            try
            {
                var settings = new GameSettings
                {
                    CommentatorVoice = form.CommentatorVoice,
                    AllowSpectators = form.AllowSpectators
                };
                var game = await _service.LaunchAsync(id, User.GetUserId(), form.Difficulty, settings);
                TempData["Success"] = $"Campaign #{game.ShortCode} launched! The server has rolled for turn order.";
                return Redirect($"/Play/{game.Id}?replay=true");
            }
            catch (Exception ex) when (IsGameError(ex)) { return HandleError(ex, id); }
        }

        // POST /Games/Delete/{id}
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _service.DeleteAsync(id, User.GetUserId());
                TempData["Success"] = "Campaign deleted.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsGameError(ex)) { return HandleError(ex, null); }
        }

        // ---- error handling ----
        private static bool IsGameError(Exception ex) =>
            ex is GameRuleException or KeyNotFoundException or UnauthorizedAccessException;

        private IActionResult HandleError(Exception ex, string? setupId)
        {
            if (ex is GameRuleException rule)
            {
                TempData["Error"] = rule.Message;
                return setupId == null
                    ? RedirectToAction(nameof(Index))
                    : RedirectToAction(nameof(Setup), new { id = setupId });
            }
            // Not found, or not your game: don't reveal which
            return NotFound();
        }
    }
}