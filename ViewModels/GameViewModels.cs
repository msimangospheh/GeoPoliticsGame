using System.ComponentModel.DataAnnotations;
using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.ViewModels
{
    // The War Room lobby
    public class LobbyViewModel
    {
        public List<Game> AllGames { get; set; } = new();
        public List<Game> Shown { get; set; } = new();
        public GameStatus? Status { get; set; }
        public string? Search { get; set; }

        public int CountOf(GameStatus status) => AllGames.Count(g => g.Status == status);
        public int Victories => CountOf(GameStatus.Won);

        public int WinRate
        {
            get
            {
                int finished = AllGames.Count(g => g.IsOver);
                return finished == 0 ? 0 : (int)Math.Round(100.0 * Victories / finished);
            }
        }

        public string AvgRoundsToWin
        {
            get
            {
                var wins = AllGames.Where(g => g.Status == GameStatus.Won).ToList();
                return wins.Count == 0 ? "-" : Math.Round(wins.Average(g => g.Round)).ToString();
            }
        }
    }

    // Step 1: choose a homeland
    public class NewCampaignViewModel
    {
        public List<Country> Countries { get; set; } = new();
        public Country? Homeland { get; set; }
        public string? Search { get; set; }
    }

    // Steps 2-3: rivals drawn, rules & launch
    public class SetupViewModel
    {
        public Game Game { get; set; } = new();
    }

    // The War Council Ledger
    public class LedgerViewModel
    {
        public Game Game { get; set; } = new();
        public List<GameEvent> Entries { get; set; } = new();
        public string? Show { get; set; }   // null = all, or "attack" / "reinforce" / "transfer"
    }

    // Landing page + Watch live page
    public class HomeViewModel
    {
        public List<Game> LiveGames { get; set; } = new();

        // A short status for each live game, e.g. ("BATTLE", "target", "Commander Sphe storms India")
        public static (string Label, string Css, string Headline) Describe(Game g)
        {
            if (g.ActiveBattle is { } battle)
            {
                var target = g.Territories.FirstOrDefault(t => t.CountryCode == battle.DefenderCode)?.CountryName
                             ?? battle.DefenderCode;
                return ("BATTLE", "target", $"{g.OwnerName} storms {target}");
            }

            int held = g.TerritoryCount(g.HumanPlayerId);
            return held >= 7
                ? ("NEARS VICTORY", "you", $"{g.OwnerName} holds {held} of 10 nations")
                : ("IN PROGRESS", "npc", $"{g.OwnerName} holds {held} nation{(held == 1 ? "" : "s")}");
        }
    }

    // The end-of-game screen ("The World Is Yours" / "Your Banner Falls")
    public class ResultViewModel
    {
        public Game Game { get; }
        public bool Won => Game.Status == GameStatus.Won;
        public string Homeland { get; }
        public int BattlesWon { get; }
        public int DiceRolled { get; }
        public int RivalsToppled { get; }
        public List<GameEvent> KeyMoments { get; }
        public List<(int Round, int Nations)> Timeline { get; }

        public ResultViewModel(Game game, List<GameEvent> events)
        {
            Game = game;
            Homeland = game.Territories.FirstOrDefault(t => t.CountryCode == game.StartCountryCode)?.CountryName
                       ?? game.StartCountryCode;

            BattlesWon = events.Count(e => e.Type == GameEventType.Conquered && e.ActorPlayerId == game.HumanPlayerId);
            DiceRolled = events.Where(e => e.Type == GameEventType.DiceRolled)
                               .Sum(e => (e.AttackerDice?.Count ?? 0) + (e.DefenderDice?.Count ?? 0));
            RivalsToppled = game.Players.Count(p => !p.IsHuman && p.IsEliminated);

            var moments = events.Where(e => e.IsKeyMoment).ToList();
            KeyMoments = moments.Count <= 6 ? moments : moments.Take(3).Concat(moments.TakeLast(3)).ToList();

            // How many countries the human held at the end of each round
            Timeline = game.History.OrderBy(s => s.Round)
                                   .Select(s => (s.Round, s.TerritoriesByPlayer.GetValueOrDefault(game.HumanPlayerId)))
                                   .ToList();
        }
    }

    // What the Launch form sends
    public class LaunchForm
    {
        [Required]
        public Difficulty Difficulty { get; set; } = Difficulty.Commander;

        [Required]
        public string CommentatorVoice { get; set; } = ChroniclerVoices.All[0];

        public bool AllowSpectators { get; set; }
    }

    public static class ChroniclerVoices
    {
        public static readonly string[] All = { "Sports broadcaster", "War poet", "Dry historian" };
    }

    // Descriptions shown on the cards
    public static class ProfileInfo
    {
        public static string Describe(NpcProfile profile) => profile switch
        {
            NpcProfile.Aggressor => "Strikes the weakest, rarely retreats.",
            NpcProfile.Defender => "Builds big garrisons, attacks late.",
            NpcProfile.Opportunist => "Waits for others to bleed.",
            _ => ""
        };

        public static string DescribeDifficulty(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Recruit => "NPCs retreat early and attack rarely.",
            Difficulty.Commander => "Balanced NPCs.",
            Difficulty.Warlord => "NPCs chase the weakest target and rarely retreat.",
            _ => ""
        };
    }
}