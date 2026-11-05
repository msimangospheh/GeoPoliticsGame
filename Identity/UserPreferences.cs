using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Identity
{
    // A player's saved settings, stored inside their document in the "users" collection
    public class UserPreferences
    {
        // Game defaults: pre-selected on the "Rules & launch" step of a new campaign
        public Difficulty DefaultDifficulty { get; set; } = Difficulty.Commander;
        public string DefaultVoice { get; set; } = "Sports broadcaster";
        public bool AllowSpectators { get; set; } = true;

        // Sound (volumes are 0-100)
        public bool SoundEnabled { get; set; } = true;
        public int SoundVolume { get; set; } = 60;
        public bool MusicEnabled { get; set; }
        public int MusicVolume { get; set; } = 35;
    }
}
