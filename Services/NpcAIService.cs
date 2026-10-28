using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Services
{
    public class NpcAIService
    {
        private readonly GameEngineService _gameEngine;
        private static readonly Random _random = new();

        public NpcAIService(GameEngineService gameEngine)
        {
            _gameEngine = gameEngine;
        }

        // Executes turns for all active NPC factions in the game
        public void SimulateNpcTurns(GameState state, string playerName)
        {
            // Find all unique NPC owners who still control at least one country
            var npcIds = state.Countries
                .Where(c => c.OwnerId != playerName && c.SoldierCount > 0)
                .Select(c => c.OwnerId)
                .Distinct()
                .ToList();

            foreach (var npcId in npcIds)
            {
                ExecuteSingleNpcTurn(state, npcId, playerName);
            }
        }

        private void ExecuteSingleNpcTurn(GameState state, string npcId, string playerName)
        {
            var npcCountries = state.Countries.Where(c => c.OwnerId == npcId).ToList();
            if (!npcCountries.Any()) return;

            // 1. Reinforcement Phase: Give NPC troops based on controlled territories ($2 \times \text{count}$)[cite: 5]
            int reinforcements = Math.Max(2, npcCountries.Count * 2);
            var strongestNpcCountry = npcCountries.OrderByDescending(c => c.SoldierCount).First();
            strongestNpcCountry.SoldierCount += reinforcements;

            state.BattleLogs.Insert(0, $"[Round {state.CurrentRound}] {npcId} deployed {reinforcements} reinforcements to {strongestNpcCountry.Name}. " +
                                       $"WHY: Bolstering strongest frontline garrison to prepare for offensive maneuvers.");

            // 2. Attack Phase: NPC evaluates targets to attack (must have > 2 soldiers)[cite: 3, 6]
            var potentialAttackers = npcCountries.Where(c => c.SoldierCount >= 2).ToList();
            if (!potentialAttackers.Any()) return;

            var attacker = potentialAttackers.OrderByDescending(c => c.SoldierCount).First();

            // Find the weakest target on the board that does NOT belong to this NPC
            var target = state.Countries
                .Where(c => c.OwnerId != npcId)
                .OrderBy(c => c.SoldierCount) // Target the weakest garrison
                .FirstOrDefault();

            if (target != null)
            {
                string justification = $"WHY: {target.Name} holds a vulnerable garrison of {target.SoldierCount} soldiers, offering the highest spoils-to-risk ratio[cite: 6].";
                state.BattleLogs.Insert(0, $"[AI Target Analysis] {npcId} targets {target.Name} using {attacker.Name}. {justification}");

                // Execute the combat simulation using the game engine
                _gameEngine.ExecuteAttack(state, attacker.Name, target.Name);
            }
        }
    }
}
