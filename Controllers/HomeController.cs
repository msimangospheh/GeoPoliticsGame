using System.Diagnostics;
using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    // Public pages: anyone can see these, logged in or not
    public class HomeController : Controller
    {
        private readonly IGameRepository _games;

        public HomeController(IGameRepository games) => _games = games;

        // GET /   - landing page with the three most recent live games
        public async Task<IActionResult> Index() =>
            View(new HomeViewModel { LiveGames = await _games.GetLiveAsync(3) });

        // GET /Home/Live   - every public game in progress
        public async Task<IActionResult> Live() =>
            View(new HomeViewModel { LiveGames = await _games.GetLiveAsync(30) });

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}