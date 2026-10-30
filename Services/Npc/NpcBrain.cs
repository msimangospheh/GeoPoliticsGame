using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services.Engine;

namespace GeoPolitics_Game.Services.Npc
{
    // How bold an NPC is. Depends on the game's difficulty AND the NPC's profile.
    public record NpcStyle(double AttackLine, double RetreatLine, double RiskWeight)
    {
        public static NpcStyle For(Difficulty difficulty, NpcProfile profile)
        {
            var (attack, retreat) = difficulty switch
            {
                Difficulty.Recruit => (0.70, 0.60),   // timid
                Difficulty.Warlord => (0.40, 0.25),   // ruthless
                _ => (0.55, 0.40)                     // Commander (balanced)
            };

            return profile switch
            {
                NpcProfile.Aggressor => new NpcStyle(Clamp(attack - 0.10), Clamp(retreat - 0.10), 0.5),
                NpcProfile.Defender => new NpcStyle(Clamp(attack + 0.10), Clamp(retreat + 0.05), 1.5),
                _ => new NpcStyle(attack, retreat, 1.0)   // Opportunist
            };
        }

        private static double Clamp(double value) => Math.Clamp(value, 0.15, 0.90);
    }

    // The real NPC: evaluates the board, plays to win, and explains every decision.
    public class NpcBrain : INpcPlayer
    {
        private const int MaxRollsPerBattle = 200;
        private readonly IGameEngine _engine;

        public NpcBrain(IGameEngine engine) => _engine = engine;

        private record AttackPlan(Territory From, Territory Target, double Odds, double Score);

        public List<GameEvent> PlayTurn(Game game)
        {
            var me = game.GetPlayer(game.CurrentPlayerId);
            var style = NpcStyle.For(game.Difficulty, me.Profile);
            var events = new List<GameEvent>();

            // 1. REINFORCE
            if (game.Phase == TurnPhase.Reinforce)
                events.AddRange(Reinforce(game, me));

            // 2. SCAN every possible attack and SCORE it
            var plans = AllAttacks(game, me, style);
            var best = plans.Where(p => p.Odds >= style.AttackLine).MaxBy(p => p.Score);

            // 3. DECIDE: attack the best target above its attack line
            if (best != null)
            {
                events.AddRange(Attack(game, me, style, best));
                if (game.Status != GameStatus.InProgress || game.CurrentPlayerId != me.Id)
                    return events;   // the game ended, or a retreat ended the turn
            }

            // 4. REGROUP
            events.AddRange(Regroup(game, me));

            // 5. END TURN (and explain a skip)
            var end = _engine.EndTurn(game, me.Id);
            if (best == null)
            {
                var closest = plans.MaxBy(p => p.Odds);
                end[0].Reason = closest == null
                    ? "None of its countries has the 2 soldiers needed to attack, so it holds."
                    : $"No target scored above its {Pct(style.AttackLine)}% attack line " +
                      $"(best: {closest.Target.CountryName} at {Pct(closest.Odds)}%), so it saves its soldiers.";
                end[0].Metrics = Metrics(("Best odds", closest?.Odds ?? 0), ("Risk", 1 - (closest?.Odds ?? 0)));
            }
            events.AddRange(end);
            return events;
        }

        // ---------- 1. Reinforce ----------
        private List<GameEvent> Reinforce(Game game, PlayerState me)
        {
            var mine = game.TerritoriesOf(me.Id);
            int amount = me.ReinforcementsToPlace;
            Territory pick;
            string reason;

            if (me.Profile == NpcProfile.Defender)
            {
                pick = mine.OrderByDescending(t => ThreatTo(game, t)).First();
                reason = $"{pick.CountryName} is in the most danger ({Pct(ThreatTo(game, pick))}% chance a rival could take it), so it gets a bigger garrison.";
            }
            else
            {
                pick = mine.OrderByDescending(t => LaunchScore(game, t, t.Soldiers + amount)).First();
                var target = game.Territories.Where(e => e.OwnerPlayerId != me.Id)
                                 .OrderByDescending(e => BattleOdds.WinProbability(pick.Soldiers + amount, e.Soldiers))
                                 .FirstOrDefault();
                reason = target == null
                    ? $"{pick.CountryName} is its strongest country."
                    : $"{pick.CountryName} is its best launch point: {Pct(BattleOdds.WinProbability(pick.Soldiers + amount, target.Soldiers))}% odds against {target.CountryName} after reinforcing.";
            }

            double frontline = LaunchScore(game, pick, pick.Soldiers + amount);
            double threat = ThreatTo(game, pick);

            var events = _engine.PlaceReinforcements(game, me.Id, new Dictionary<string, int> { [pick.CountryCode] = amount });
            var e = events.First(x => x.Type == GameEventType.Reinforced);
            e.Message = $"Places {amount} soldiers on {pick.CountryName} (now {pick.Soldiers}).";
            e.Reason = reason;
            e.Metrics = Metrics(("Frontline", frontline), ("Threat", threat));
            return events;
        }

        // ---------- 2. Scan and score ----------
        private static List<AttackPlan> AllAttacks(Game game, PlayerState me, NpcStyle style)
        {
            var leaderId = LeaderId(game, me);
            var plans = new List<AttackPlan>();

            foreach (var from in game.TerritoriesOf(me.Id).Where(t => t.CanAttack))
                foreach (var target in game.Territories.Where(t => t.OwnerPlayerId != me.Id))
                {
                    double odds = BattleOdds.WinProbability(from.Soldiers, target.Soldiers);
                    plans.Add(new AttackPlan(from, target, odds, Score(game, me, style, target, odds, leaderId)));
                }
            return plans;
        }

        // odds x value of winning, minus the risk to its own army
        private static double Score(Game game, PlayerState me, NpcStyle style, Territory target, double odds, string? leaderId)
        {
            double value = 1 + target.ConquestSpoils / 4.0;                              // spoils are worth 2-4
            if (target.OwnerPlayerId == leaderId) value += 0.5;                          // stop the board leader
            if (game.TerritoryCount(target.OwnerPlayerId) == 1) value += 0.5;            // knocks a rival out
            if (me.Profile == NpcProfile.Aggressor && target.Soldiers <= 3) value += 0.3;  // strikes the weakest
            if (me.Profile == NpcProfile.Opportunist && target.Soldiers < target.SoldierValue) value += 0.4; // already bled

            return odds * value - (1 - odds) * style.RiskWeight;
        }

        // ---------- 3 + 4. Attack, re-checking the odds after every roll ----------
        private List<GameEvent> Attack(Game game, PlayerState me, NpcStyle style, AttackPlan plan)
        {
            var from = plan.From;
            var target = plan.Target;
            var leaderId = LeaderId(game, me);
            var targetOwner = game.GetPlayer(target.OwnerPlayerId);

            var why = new List<string> { $"{Pct(plan.Odds)}% odds to win +{target.ConquestSpoils} soldiers" };
            if (target.OwnerPlayerId == leaderId) why.Add($"{targetOwner.Name} leads the board");
            if (game.TerritoryCount(target.OwnerPlayerId) == 1) why.Add($"it would knock {targetOwner.Name} out");

            var events = _engine.StartAttack(game, me.Id, from.CountryCode, target.CountryCode);
            var start = events.First(e => e.Type == GameEventType.AttackStarted);
            start.Message = $"Attacks {target.CountryName} from {from.CountryName}.";
            start.Reason = $"{target.CountryName} is its best target: {string.Join("; ", why)}.";
            start.Metrics = Metrics(("Odds", plan.Odds), ("Spoils", target.ConquestSpoils / 4.0));

            for (int roll = 1; roll <= MaxRollsPerBattle && game.ActiveBattle != null; roll++)
            {
                var rolled = _engine.Roll(game, me.Id);
                events.AddRange(rolled);
                if (game.ActiveBattle == null) return events;   // conquered, or failed at 1 soldier

                double odds = BattleOdds.WinProbability(from.Soldiers, target.Soldiers);
                if (odds < style.RetreatLine)
                {
                    events.AddRange(RetreatFrom(game, me, from, target,
                        $"Stops its attack on {target.CountryName} after roll {roll}.",
                        $"Odds fell from {Pct(plan.Odds)}% to {Pct(odds)}%, under its {Pct(style.RetreatLine)}% retreat line, so it keeps {from.Soldiers} soldiers.",
                        odds));
                    return events;
                }

                // Keep going - and say why
                var diceEvent = rolled.First(e => e.Type == GameEventType.DiceRolled);
                diceEvent.Reason = $"Odds still {Pct(odds)}%, above its {Pct(style.RetreatLine)}% retreat line, so it keeps attacking.";
                diceEvent.Metrics = Metrics(("Odds", odds), ("Risk", 1 - odds));
            }

            if (game.ActiveBattle != null)   // safety net: endless ties
                events.AddRange(RetreatFrom(game, me, from, target,
                    $"Calls off the attack on {target.CountryName}.",
                    "The battle dragged on too long, so it stops to save its soldiers.",
                    BattleOdds.WinProbability(from.Soldiers, target.Soldiers)));

            return events;
        }

        private List<GameEvent> RetreatFrom(Game game, PlayerState me, Territory from, Territory target,
                                            string message, string reason, double odds)
        {
            var events = _engine.Retreat(game, me.Id);   // ends its turn (exam rule)
            var r = events.First(e => e.Type == GameEventType.Retreated);
            r.Message = message;
            r.Reason = reason;
            r.Metrics = Metrics(("Odds", odds), ("Risk", 1 - odds));
            return events;
        }

        // ---------- 5. Regroup ----------
        private List<GameEvent> Regroup(Game game, PlayerState me)
        {
            var mine = game.TerritoriesOf(me.Id);
            if (mine.Count < 2) return new List<GameEvent>();

            var safest = mine.OrderBy(t => ThreatTo(game, t)).ThenByDescending(t => t.Soldiers).First();
            var weakest = mine.Where(t => t != safest).OrderByDescending(t => ThreatTo(game, t)).First();

            int keep = me.Profile == NpcProfile.Defender ? 3 : 2;   // Defenders keep bigger garrisons
            int spare = safest.Soldiers - keep;
            double safeThreat = ThreatTo(game, safest);
            double weakThreat = ThreatTo(game, weakest);
            if (spare < 1 || weakThreat - safeThreat < 0.2) return new List<GameEvent>();

            int amount = Math.Max(1, spare / 2);
            var events = _engine.Transfer(game, me.Id, safest.CountryCode, weakest.CountryCode, amount);
            var e = events.First();
            e.Message = $"Moves {amount} soldier{(amount == 1 ? "" : "s")} {safest.CountryName} -> {weakest.CountryName}.";
            e.Reason = $"{weakest.CountryName} is its most threatened country ({Pct(weakThreat)}% danger), while {safest.CountryName} is safe.";
            e.Metrics = Metrics(("Garrison", weakest.Soldiers / (double)Math.Max(1, game.Territories.Max(t => t.Soldiers))),
                                ("Threat", weakThreat));
            return events;
        }

        // ---------- helpers ----------

        // Chance that the strongest rival army could take this country
        private static double ThreatTo(Game game, Territory t) =>
            game.Territories.Where(e => e.OwnerPlayerId != t.OwnerPlayerId)
                .Select(e => BattleOdds.WinProbability(e.Soldiers, t.Soldiers))
                .DefaultIfEmpty(0).Max();

        // Best chance of conquering something from here with this many soldiers
        private static double LaunchScore(Game game, Territory from, int soldiers) =>
            game.Territories.Where(e => e.OwnerPlayerId != from.OwnerPlayerId)
                .Select(e => BattleOdds.WinProbability(soldiers, e.Soldiers))
                .DefaultIfEmpty(0).Max();

        // The rival with the most countries (then most soldiers)
        private static string? LeaderId(Game game, PlayerState me) =>
            game.Players.Where(p => !p.IsEliminated && p.Id != me.Id)
                        .OrderByDescending(p => game.TerritoryCount(p.Id))
                        .ThenByDescending(p => game.SoldierCount(p.Id))
                        .Select(p => p.Id)
                        .FirstOrDefault();

        private static int Pct(double p) => (int)Math.Round(p * 100);

        private static List<DecisionMetric> Metrics(params (string Label, double Value)[] items) =>
            items.Select(i => new DecisionMetric { Label = i.Label, Percent = Math.Clamp(Pct(i.Value), 0, 100) }).ToList();
    }
}