using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GeoPolitics_Game.Models
{
    public enum GameStatus { Setup, InProgress, Won, Lost }
    public enum NpcProfile { None, Aggressor, Defender, Opportunist }
    public enum TurnPhase { Reinforce, Main, Battle, Finished }
    public enum Difficulty { Recruit, Commander, Warlord }   // Easy / Normal / Hard

    // One game session (stored in the "games" collection)
    public class Game
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public string OwnerUserId { get; set; } = string.Empty;      // who may play this game
        public string OwnerName { get; set; } = string.Empty;        // "Commander Sphe"
        public string HumanPlayerId { get; set; } = string.Empty;
        public string StartCountryCode { get; set; } = string.Empty;

        public Difficulty Difficulty { get; set; } = Difficulty.Commander;
        public GameSettings Settings { get; set; } = new();

        public GameStatus Status { get; set; } = GameStatus.Setup;
        public int Round { get; set; } = 1;
        public TurnPhase Phase { get; set; } = TurnPhase.Main;
        public bool AttackUsedThisTurn { get; set; }                  // "1 attack left"
        public Dictionary<string, int> PlacedThisTurn { get; set; } = new(); // "+2" badges

        public List<string> TurnOrder { get; set; } = new();          // player ids, shuffled
        public int CurrentTurnIndex { get; set; }

        public List<PlayerState> Players { get; set; } = new();
        public List<Territory> Territories { get; set; } = new();
        public BattleState? ActiveBattle { get; set; }                // null when no attack is running
        public List<RoundSnapshot> History { get; set; } = new();     // "Territory over time" chart

        public string? WinnerPlayerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }

        [BsonIgnore] public string CurrentPlayerId => TurnOrder.Count == 0 ? "" : TurnOrder[CurrentTurnIndex];
        [BsonIgnore] public string ShortCode => Id[^4..].ToUpperInvariant();   // "#A41F"
    }

    // Choices made on the New Campaign screen
    public class GameSettings
    {
        public string CommentatorVoice { get; set; } = "Sports broadcaster"; // or "War poet", "Dry historian"
        public bool AllowSpectators { get; set; } = true;
        public bool ShowOddsMeter { get; set; } = true;
    }

    // A player inside a game (you or an NPC)
    public class PlayerState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = string.Empty;          // "Commander Sphe" or "The Aggressor"
        public string HomeCountryCode { get; set; } = string.Empty;
        public bool IsHuman { get; set; }
        public NpcProfile Profile { get; set; } = NpcProfile.None;
        public bool IsEliminated { get; set; }
        public int ReinforcementsToPlace { get; set; }

        [BsonIgnore] public string Badge => IsHuman ? "Y" : Profile.ToString()[..1];   // Y / A / D / O
        [BsonIgnore] public string ColorClass => IsHuman ? "you" : "npc";
    }

    // One of the 10 countries on the board
    public class Territory
    {
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public int SoldierValue { get; set; }         // original value (for conquest spoils)
        public string OwnerPlayerId { get; set; } = string.Empty;
        public int Soldiers { get; set; }
        public int Slot { get; set; }                 // 0-9: position on the board
    }

    // An attack in progress (saved after every roll, so closing the browser can't cheat it)
    public class BattleState
    {
        public string AttackerCode { get; set; } = string.Empty;
        public string DefenderCode { get; set; } = string.Empty;
        public string AttackerPlayerId { get; set; } = string.Empty;
        public string DefenderPlayerId { get; set; } = string.Empty;
        public int AttackerStartSoldiers { get; set; }
        public int DefenderStartSoldiers { get; set; }
        public List<BattleRoll> Rolls { get; set; } = new();     // the "Battle Log" panel
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    }

    // One roll inside a battle
    public class BattleRoll
    {
        public List<int> AttackerDice { get; set; } = new();
        public List<int> DefenderDice { get; set; } = new();
        public int AttackerLosses { get; set; }
        public int DefenderLosses { get; set; }
        public int AttackerSoldiersAfter { get; set; }
        public int DefenderSoldiersAfter { get; set; }
        public double OddsAfter { get; set; }                    // "Odds now 24%"
        public string? Commentary { get; set; }                  // "The Chronicler Speaks"
    }

    // Who owned how many countries at the end of each round
    public class RoundSnapshot
    {
        public int Round { get; set; }
        public Dictionary<string, int> TerritoriesByPlayer { get; set; } = new();
    }
}
