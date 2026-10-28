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

        public string OwnerUserId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string HumanPlayerId { get; set; } = string.Empty;
        public string StartCountryCode { get; set; } = string.Empty;

        public Difficulty Difficulty { get; set; } = Difficulty.Commander;
        public GameSettings Settings { get; set; } = new();

        public GameStatus Status { get; set; } = GameStatus.Setup;
        public int Round { get; set; } = 1;
        public TurnPhase Phase { get; set; } = TurnPhase.Main;
        public int MoveInTurn { get; set; }                           // "R5 · move 2"
        public bool AttackUsedThisTurn { get; set; }                  // "1 attack left"
        public Dictionary<string, int> PlacedThisTurn { get; set; } = new(); // "+2" badges

        public List<string> TurnOrder { get; set; } = new();
        public int CurrentTurnIndex { get; set; }

        public List<PlayerState> Players { get; set; } = new();
        public List<Territory> Territories { get; set; } = new();
        public BattleState? ActiveBattle { get; set; }
        public List<RoundSnapshot> History { get; set; } = new();

        public string? WinnerPlayerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }

        [BsonIgnore] public string CurrentPlayerId => TurnOrder.Count == 0 ? "" : TurnOrder[CurrentTurnIndex];
        [BsonIgnore] public string ShortCode => Id[^4..].ToUpperInvariant();   // "#A41F"
        [BsonIgnore] public bool IsOver => Status is GameStatus.Won or GameStatus.Lost;

        // ---------- Rules helpers: the game answers questions about itself ----------

        public PlayerState GetPlayer(string playerId) =>
            Players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new GameRuleException("That player is not in this game.");

        public Territory GetTerritory(string countryCode) =>
            Territories.FirstOrDefault(t => t.CountryCode == countryCode?.ToUpperInvariant())
            ?? throw new GameRuleException($"{countryCode} is not on this board.");

        public List<Territory> TerritoriesOf(string playerId) =>
            Territories.Where(t => t.OwnerPlayerId == playerId).ToList();

        public int TerritoryCount(string playerId) =>
            Territories.Count(t => t.OwnerPlayerId == playerId);

        public int SoldierCount(string playerId) =>
            Territories.Where(t => t.OwnerPlayerId == playerId).Sum(t => t.Soldiers);

        public void EnsureTurnOf(string playerId)
        {
            if (Status != GameStatus.InProgress) throw new GameRuleException("This game is not in progress.");
            if (CurrentPlayerId != playerId) throw new GameRuleException("It is not your turn.");
        }

        // Records who owns how many countries (for the "territory over time" chart)
        public void TakeSnapshot()
        {
            History.RemoveAll(s => s.Round == Round);
            History.Add(new RoundSnapshot
            {
                Round = Round,
                TerritoriesByPlayer = Players.ToDictionary(p => p.Id, p => TerritoryCount(p.Id))
            });
        }
    }

    public class GameSettings
    {
        public string CommentatorVoice { get; set; } = "Sports broadcaster"; // or "War poet", "Dry historian"
        public bool AllowSpectators { get; set; } = true;
        public bool ShowOddsMeter { get; set; } = true;
    }

    public class PlayerState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = string.Empty;
        public string HomeCountryCode { get; set; } = string.Empty;
        public bool IsHuman { get; set; }
        public NpcProfile Profile { get; set; } = NpcProfile.None;
        public bool IsEliminated { get; set; }
        public int ReinforcementsToPlace { get; set; }

        [BsonIgnore] public string Badge => IsHuman ? "Y" : Profile.ToString()[..1];   // Y / A / D / O
        [BsonIgnore] public string ColorClass => IsHuman ? "you" : "npc";
    }

    public class Territory
    {
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public int SoldierValue { get; set; }
        public string OwnerPlayerId { get; set; } = string.Empty;
        public int Soldiers { get; set; }
        public int Slot { get; set; }

        // Rule: you need at least 2 soldiers to attack or keep attacking
        [BsonIgnore] public bool CanAttack => Soldiers >= 2;

        // Rule: conquering pays max(2, soldier value / 2), rounded down
        [BsonIgnore] public int ConquestSpoils => Math.Max(2, SoldierValue / 2);

        // Rule: dice = the smaller of (attackers - 1) and defenders
        public int DiceAgainst(Territory defender) => Math.Min(Soldiers - 1, defender.Soldiers);

        public void AddSoldiers(int amount)
        {
            if (amount < 0) throw new GameRuleException("Cannot add a negative number of soldiers.");
            Soldiers += amount;
        }

        public void LoseSoldiers(int amount) => Soldiers = Math.Max(0, Soldiers - amount);

        // Rule: transfers only between your own countries, and at least 1 soldier must stay behind
        public void TransferTo(Territory target, int amount)
        {
            if (target.CountryCode == CountryCode) throw new GameRuleException("Choose two different countries.");
            if (target.OwnerPlayerId != OwnerPlayerId) throw new GameRuleException("You can only transfer between your own countries.");
            if (amount < 1) throw new GameRuleException("Transfer at least 1 soldier.");
            if (Soldiers - amount < 1) throw new GameRuleException($"{CountryName} must keep at least 1 soldier.");
            Soldiers -= amount;
            target.Soldiers += amount;
        }

        public void CaptureBy(string newOwnerId, int garrison)
        {
            OwnerPlayerId = newOwnerId;
            Soldiers = garrison;
        }
    }

    public class BattleState
    {
        public string AttackerCode { get; set; } = string.Empty;
        public string DefenderCode { get; set; } = string.Empty;
        public string AttackerPlayerId { get; set; } = string.Empty;
        public string DefenderPlayerId { get; set; } = string.Empty;
        public int AttackerStartSoldiers { get; set; }
        public int DefenderStartSoldiers { get; set; }
        public List<BattleRoll> Rolls { get; set; } = new();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    }

    public class BattleRoll
    {
        public List<int> AttackerDice { get; set; } = new();
        public List<int> DefenderDice { get; set; } = new();
        public int AttackerLosses { get; set; }
        public int DefenderLosses { get; set; }
        public int AttackerSoldiersAfter { get; set; }
        public int DefenderSoldiersAfter { get; set; }
        public double OddsAfter { get; set; }
        public string? Commentary { get; set; }
    }

    public class RoundSnapshot
    {
        public int Round { get; set; }
        public Dictionary<string, int> TerritoriesByPlayer { get; set; } = new();
    }
}