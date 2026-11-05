using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.Security;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    [AllowAnonymous]   // anyone can see the Hall of Commanders
    public class LeaderboardController : Controller
    {
        private const int MaxRows = 50;
        private readonly ILeaderboardRepository _board;

        public LeaderboardController(ILeaderboardRepository board) => _board = board;

        // GET /Leaderboard?difficulty=Warlord&search=sphe
        public async Task<IActionResult> Index(Difficulty? difficulty, string? search)
        {
            if (difficulty.HasValue && !Enum.IsDefined(difficulty.Value)) difficulty = null;
            if (search?.Length > 40) search = search[..40];   // keep searches short

            var model = new LeaderboardViewModel
            {
                Rows = await _board.GetTopAsync(difficulty, search, MaxRows),
                Stats = await _board.GetPlatformStatsAsync(difficulty),
                Difficulty = difficulty,
                Search = search,
                CurrentUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : string.Empty
            };
            return View(model);
        }
    }
}
