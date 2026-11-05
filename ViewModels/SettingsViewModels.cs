using System.ComponentModel.DataAnnotations;
using GeoPolitics_Game.Identity;
using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.ViewModels
{
    // The whole Settings page: three separate forms, each saved on its own
    public class SettingsViewModel
    {
        public string Email { get; set; } = string.Empty;
        public ProfileForm Profile { get; set; } = new();
        public PreferencesForm Preferences { get; set; } = new();
        public ChangePasswordForm Password { get; set; } = new();
        public bool ApplySoundNow { get; set; }   // true right after saving: push the sound settings into this browser
    }

    public class ProfileForm
    {
        [Required, StringLength(20, MinimumLength = 2)]
        [RegularExpression(@"^[A-Za-z0-9 _-]+$", ErrorMessage = "Use letters, numbers, spaces, - or _ only.")]
        [Display(Name = "Commander name")]
        public string DisplayName { get; set; } = string.Empty;
    }

    public class PreferencesForm
    {
        [Display(Name = "Default difficulty")]
        public Difficulty DefaultDifficulty { get; set; } = Difficulty.Commander;

        [Required, Display(Name = "Default chronicler voice")]
        public string DefaultVoice { get; set; } = ChroniclerVoices.All[0];

        [Display(Name = "Allow spectators by default")]
        public bool AllowSpectators { get; set; } = true;

        [Display(Name = "Sound effects")]
        public bool SoundEnabled { get; set; } = true;

        [Range(0, 100), Display(Name = "Effects volume")]
        public int SoundVolume { get; set; } = 60;

        [Display(Name = "Background music")]
        public bool MusicEnabled { get; set; }

        [Range(0, 100), Display(Name = "Music volume")]
        public int MusicVolume { get; set; } = 35;

        public static PreferencesForm From(UserPreferences p) => new()
        {
            DefaultDifficulty = p.DefaultDifficulty,
            DefaultVoice = p.DefaultVoice,
            AllowSpectators = p.AllowSpectators,
            SoundEnabled = p.SoundEnabled,
            SoundVolume = p.SoundVolume,
            MusicEnabled = p.MusicEnabled,
            MusicVolume = p.MusicVolume
        };

        public UserPreferences ToPreferences() => new()
        {
            DefaultDifficulty = DefaultDifficulty,
            DefaultVoice = DefaultVoice,
            AllowSpectators = AllowSpectators,
            SoundEnabled = SoundEnabled,
            SoundVolume = SoundVolume,
            MusicEnabled = MusicEnabled,
            MusicVolume = MusicVolume
        };
    }

    public class ChangePasswordForm
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // The Leaderboard page
    public class LeaderboardViewModel
    {
        public List<LeaderboardEntry> Rows { get; set; } = new();
        public PlatformStats Stats { get; set; } = new();
        public Difficulty? Difficulty { get; set; }
        public string? Search { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public LeaderboardEntry? Me => Rows.FirstOrDefault(r => r.UserId == CurrentUserId);
    }
}
