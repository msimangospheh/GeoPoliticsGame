using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    public class GameController : Controller
    {
        private readonly GameEngineService _gameEngine;
        private readonly NpcAIService _npcAI;
        // For simplicity in this demo, we store a static instance in memory. 
        // Later we will connect this directly to Firebase NoSQL!
        private static GameState _currentGame = null!;

        public GameController(GameEngineService gameEngine, NpcAIService npcAI)
        {
            _gameEngine = gameEngine;
            _npcAI = npcAI;
        }

        // GET: /Game/Setup (Shows the country selection screen)
        public IActionResult Setup()
        {
            var countries = _gameEngine.GetMasterCountryList();
            return View(countries);
        }

        // POST: /Game/Create (Processes user choice and initializes game)
        [HttpPost]
        public IActionResult Create(string chosenCountry)
        {
            _currentGame = _gameEngine.InitializeGameWithChoice("Commander Sphe", chosenCountry);
            return RedirectToAction("Start");
        }

        // GET: /Game/Start (The main game board)
        public IActionResult Start()
        {
            if (_currentGame == null)
            {
                return RedirectToAction("Setup"); // If no game exists, redirect to setup
            }
            return View(_currentGame);
        }

        [HttpPost]
        public IActionResult Attack(string attackerCountry, string targetCountry)
        {
            if (_currentGame != null && !string.IsNullOrEmpty(attackerCountry) && !string.IsNullOrEmpty(targetCountry))
            {
                // 1. Player executes attack
                _gameEngine.ExecuteAttack(_currentGame, attackerCountry, targetCountry);

                // 2. Automatically simulate NPC turns right after player action
                _npcAI.SimulateNpcTurns(_currentGame, _currentGame.PlayerName);

                // 3. Increment round counter after full cycle
                _currentGame.CurrentRound++;
            }
            return RedirectToAction("Start");
        }
    }
}
