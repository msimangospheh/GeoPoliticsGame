using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GeoPolitics_Game.Models
{
    public enum GameEventType
    {
        GameStarted, TurnStarted, Reinforced, AttackStarted, DiceRolled,
        Conquered, AttackFailed, Retreated, Transferred, TurnSkipped,
        PlayerEliminated, GameWon, GameLost
    }

    // One bar on a ledger card, e.g. "Odds 71%"
    public class DecisionMetric
    {
        public string Label { get; set; } = string.Empty;
        public int Percent { get; set; }   // 0-100
    }

    // One thing that happened in a game (stored in the "gameEvents" collection)
    public class GameEvent
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string GameId { get; set; } = string.Empty;

        public int Seq { get; set; }                  // 1, 2, 3... order within the game
        public int Round { get; set; }
        public int Move { get; set; }                 // "R5 · move 2"
        public GameEventType Type { get; set; }
        public string ActorPlayerId { get; set; } = string.Empty;
        public bool IsNpc { get; set; }

        public string Message { get; set; } = string.Empty;   // "Places 6 soldiers on India (now 14)"
        public string? Reason { get; set; }                   // the "Why:" line
        public List<DecisionMetric>? Metrics { get; set; }    // the two bars
        public string? Commentary { get; set; }               // AI commentary
        public bool IsKeyMoment { get; set; }                 // shown under "Key moments"

        public string? FromCode { get; set; }
        public string? ToCode { get; set; }
        public int? Amount { get; set; }
        public List<int>? AttackerDice { get; set; }
        public List<int>? DefenderDice { get; set; }
        public int? AttackerLosses { get; set; }
        public int? DefenderLosses { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonIgnore]   // the coloured tag on ledger cards: ATTACK / REINFORCE / TRANSFER
        public string Category => Type switch
        {
            GameEventType.Reinforced => "reinforce",
            GameEventType.Transferred => "transfer",
            GameEventType.AttackStarted or GameEventType.DiceRolled or GameEventType.Conquered
                or GameEventType.AttackFailed or GameEventType.Retreated => "attack",
            _ => "other"
        };
    }
}
