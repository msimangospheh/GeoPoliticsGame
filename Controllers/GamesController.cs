using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    // [Authorize] = only logged-in users. Visitors are sent to the login page.
    [Authorize]
    public class GamesController : Controller
    {
        private readonly IGameRepository _games;

        public GamesController(IGameRepository games) => _games = games;

        // GET /Games  ("The War Room - My campaigns")
        public async Task<IActionResult> Index()
        {
            var myGames = await _games.GetByOwnerAsync(User.GetUserId());   // only MY games
            return View(myGames);
        }
    }
}