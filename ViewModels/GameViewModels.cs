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