using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services.Engine;

namespace GeoPolitics_Game.ViewModels
{
    // Everything the board and battle pages need, worked out once
    public class BoardViewModel
    {
        // Where each of the 10 slots sits on the board (percent from left, percent from top)
        private static readonly (int X, int Y)[] Slots =
        {
            (16, 20), (45, 15), (78, 20),
            (15, 50), (46, 52), (79, 50),
            (16, 82), (39, 85), (62, 85), (85, 82)
        };

        public Game Game { get; }
        public bool IsOwner { get; }
        public bool CanPlay { get; }
        public PlayerState Human { get; }
        public Territory? From { get; }
        public Territory? To { get; }
        public List<GameEvent> NpcLog { get; }
        public int MaxSoldiers { get; }

        public BoardViewModel(Game game, bool isOwner, string? fromCode, string? toCode, List<GameEvent> events)
        {
            Game = game;
            IsOwner = isOwner;
            Human = game.GetPlayer(game.HumanPlayerId);

            // Buttons only for the owner, only on their turn, only while the game is running
            CanPlay = isOwner && game.Status == GameStatus.InProgress && game.CurrentPlayerId == game.HumanPlayerId;

            // Planned attack: "from" must be yours, "to" must be a rival's
            From = game.Territories.FirstOrDefault(t => t.CountryCode == fromCode && t.OwnerPlayerId == Human.Id)
                   ?? game.TerritoriesOf(Human.Id).OrderByDescending(t => t.Soldiers).FirstOrDefault();
            To = game.Territories.FirstOrDefault(t => t.CountryCode == toCode && t.OwnerPlayerId != Human.Id);

            // Latest NPC decisions that have a reason (dice rolls are left for the full ledger)
            NpcLog = events.Where(e => e.IsNpc && !string.IsNullOrEmpty(e.Reason) && e.Type != GameEventType.DiceRolled)
                           .Reverse().Take(3).ToList();

            MaxSoldiers = Math.Max(1, game.Territories.Max(t => t.Soldiers));
        }

        public static (int X, int Y) SlotPosition(int slot) => Slots[slot % Slots.Length];

        public PlayerState OwnerOf(Territory t) => Game.GetPlayer(t.OwnerPlayerId);
        public bool IsMine(Territory t) => t.OwnerPlayerId == Human.Id;
        public int PlacedThisTurn(Territory t) => Game.PlacedThisTurn.GetValueOrDefault(t.CountryCode);
        public int BarPercent(Territory t) => 100 * t.Soldiers / MaxSoldiers;

        public int OddsPercent => From != null && To != null ? BattleOdds.AsPercent(From.Soldiers, To.Soldiers) : 0;

        // Bottom bar: You, then each NPC profile added together
        public IEnumerable<(string Name, string Css, int Nations, int Soldiers)> Standings()
        {
            yield return ("You", "you", Game.TerritoryCount(Human.Id), Game.SoldierCount(Human.Id));
            foreach (var profile in new[] { NpcProfile.Aggressor, NpcProfile.Defender, NpcProfile.Opportunist })
            {
                var ids = Game.Players.Where(p => p.Profile == profile).Select(p => p.Id).ToHashSet();
                if (ids.Count == 0) continue;
                var owned = Game.Territories.Where(t => ids.Contains(t.OwnerPlayerId)).ToList();
                yield return ($"The {profile}", "npc", owned.Count, owned.Sum(t => t.Soldiers));
            }
        }

        // ---------- Battle helpers ----------
        public BattleState? Battle => Game.ActiveBattle;
        public Territory? Attacker => Battle == null ? null : Game.GetTerritory(Battle.AttackerCode);
        public Territory? Defender => Battle == null ? null : Game.GetTerritory(Battle.DefenderCode);
        public BattleRoll? LastRoll => Battle?.Rolls.LastOrDefault();

        // Placeholder commentary until the AI chronicler arrives in Phase 7
        public string ChronicleLine()
        {
            var roll = LastRoll;
            if (roll == null || Attacker == null || Defender == null)
                return "The armies stand ready. The first dice are about to fall.";
            if (!string.IsNullOrWhiteSpace(roll.Commentary))
                return roll.Commentary;

            if (roll.DefenderLosses > roll.AttackerLosses)
                return $"{Attacker.CountryName} presses forward! {Defender.CountryName} loses {roll.DefenderLosses} in a single roll.";
            if (roll.AttackerLosses > roll.DefenderLosses)
                return $"{Defender.CountryName} holds firm, and {Attacker.CountryName} pays with {roll.AttackerLosses} soldier{(roll.AttackerLosses == 1 ? "" : "s")}.";
            return "Blow for blow! Neither side gives an inch.";
        }
    }
}