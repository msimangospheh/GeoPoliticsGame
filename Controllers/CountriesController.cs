using GeoPolitics_Game.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    public class CountriesController : Controller
    {
        private readonly ICountryRepository _countries;

        public CountriesController(ICountryRepository countries) => _countries = countries;

        // /Countries?search=south
        public async Task<IActionResult> Index(string? search)
        {
            ViewData["Search"] = search;
            var list = await _countries.GetAllAsync(search);
            return View(list);
        }
    }
}
