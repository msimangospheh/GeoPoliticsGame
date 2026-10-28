using GeoPolitics_Game.Models; // Ensure this matches your project's actual namespace

namespace GeoPolitics_Game.Services
{
    public class GameEngineService
    {
        private static readonly Random _random = new();

        // 1. Master list of countries and their starting soldiers based on population tiers[cite: 1, 2]
        public List<(string Name, int Soldiers)> GetMasterCountryList()
        {
            return new List<(string Name, int Soldiers)>
            {
                ("South Africa", 5), ("India", 9), ("Vatican City", 1),
                ("Germany", 6), ("Egypt", 7), ("Nigeria", 9),
                ("Brazil", 9), ("Australia", 4), ("Canada", 4), ("Japan", 7)
            };
        }

        // 2. Initialize game with the player's chosen country + 9 random NPCs[cite: 1]
        public GameState InitializeGameWithChoice(string playerName, string chosenCountryName)
        {
            var masterList = GetMasterCountryList();
            var chosen = masterList.FirstOrDefault(c => c.Name == chosenCountryName);
            if (string.IsNullOrEmpty(chosen.Name)) chosen = masterList[0]; // Fallback safety

            // Get the remaining countries and pick 9 randomly for the NPCs[cite: 1]
            var remaining = masterList.Where(c => c.Name != chosen.Name).OrderBy(_ => _random.Next()).Take(9).ToList();

            var countries = new List<Country>
            {
                new Country { Name = chosen.Name, OwnerId = playerName, SoldierCount = chosen.Soldiers, PopulationTier = chosen.Soldiers }
            };

            string[] npcs = { "NPC_Aggressor", "NPC_Defender", "NPC_Opportunist" };
            for (int i = 0; i < remaining.Count; i++)
            {
                countries.Add(new Country
                {
                    Name = remaining[i].Name,
                    OwnerId = npcs[i % npcs.Length],
                    SoldierCount = remaining[i].Soldiers,
                    PopulationTier = remaining[i].Soldiers
                });
            }

            return new GameState
            {
                PlayerName = playerName,
                CurrentRound = 1,
                Countries = countries,
                BattleLogs = { $"Campaign started! You lead {chosen.Name}. Select a target to attack!" }
            };
        }

        public void ExecuteAttack(GameState state, string attackerName, string defenderName)
        {
            var attacker = state.Countries.FirstOrDefault(c => c.Name == attackerName);
            var defender = state.Countries.FirstOrDefault(c => c.Name == defenderName);

            if (attacker == null || defender == null) return;

            if (attacker.SoldierCount < 2)
            {
                state.BattleLogs.Insert(0, $"Attack failed: {attackerName} must have at least 2 soldiers to attack.");
                return;
            }

            int attackingSoldiersAvailable = attacker.SoldierCount - 1;
            int numDice = Math.Min(attackingSoldiersAvailable, defender.SoldierCount);

            var attackerRolls = Enumerable.Range(0, numDice).Select(_ => _random.Next(1, 7)).OrderByDescending(x => x).ToList();
            var defenderRolls = Enumerable.Range(0, numDice).Select(_ => _random.Next(1, 7)).OrderByDescending(x => x).ToList();

            int attackerLosses = 0;
            int defenderLosses = 0;

            for (int i = 0; i < numDice; i++)
            {
                if (attackerRolls[i] > defenderRolls[i])
                {
                    defenderLosses++;
                }
                else
                {
                    defenderLosses++; // Ties go to defender
                }
            }

            attacker.SoldierCount -= attackerLosses;
            defender.SoldierCount -= defenderLosses;

            // FIXED: Use C# string interpolation ($"...") instead of backticks
            string log = $"[Round {state.CurrentRound}] {attackerName} attacked {defenderName}. " +
                         $"Dice - Attacker: [{string.Join(", ", attackerRolls)}] vs Defender: [{string.Join(", ", defenderRolls)}]. " +
                         $"Losses: Attacker -{attackerLosses}, Defender -{defenderLosses}.";
            state.BattleLogs.Insert(0, log);

            if (defender.SoldierCount <= 0)
            {
                defender.OwnerId = attacker.OwnerId;
                int reward = Math.Max(2, defender.PopulationTier / 2);
                defender.SoldierCount = reward;

                state.BattleLogs.Insert(0, $"VICTORY! {defenderName} was conquered! {attacker.OwnerId} gained {reward} replacement soldiers.");
            }
        }
    }
}