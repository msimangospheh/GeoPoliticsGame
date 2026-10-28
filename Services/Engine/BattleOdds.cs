using System.Collections.Concurrent;

namespace GeoPolitics_Game.Services.Engine
{
    // Exact probability that an attack succeeds, fighting until the end.
    // One duel (die vs die): attacker wins 15/36, defender wins 15/36, tie 6/36.
    public static class BattleOdds
    {
        private const double AttackerWins = 15.0 / 36;
        private const double DefenderWins = 15.0 / 36;
        private const double Tie = 6.0 / 36;

        // Remember answers we've already worked out (memoisation)
        private static readonly ConcurrentDictionary<(int, int), double> Cache = new();

        public static double WinProbability(int attackers, int defenders)
        {
            if (defenders <= 0) return 1.0;
            if (attackers <= 1) return 0.0;
            if (attackers + defenders > 200) return Simulate(attackers, defenders);
            if (Cache.TryGetValue((attackers, defenders), out var known)) return known;

            int dice = Math.Min(attackers - 1, defenders);
            double total = 0;

            for (int k = 0; k <= dice; k++)              // k = duels the attacker wins
            {
                double chooseK = Binomial(dice, k);
                for (int j = 0; j <= dice - k; j++)      // j = duels the defender wins
                {
                    int ties = dice - k - j;
                    if (ties == dice) continue;          // all ties: nothing changes, roll again
                    double chance = chooseK * Binomial(dice - k, j)
                                  * Math.Pow(AttackerWins, k)
                                  * Math.Pow(DefenderWins, j)
                                  * Math.Pow(Tie, ties);
                    total += chance * WinProbability(attackers - j, defenders - k);
                }
            }

            double result = total / (1 - Math.Pow(Tie, dice));
            Cache[(attackers, defenders)] = result;
            return result;
        }

        public static int AsPercent(int attackers, int defenders) =>
            (int)Math.Round(WinProbability(attackers, defenders) * 100);

        private static double Binomial(int n, int k)
        {
            double result = 1;
            for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
            return result;
        }

        // For huge armies: estimate by playing 3000 quick battles
        private static double Simulate(int attackers, int defenders)
        {
            int wins = 0;
            for (int game = 0; game < 3000; game++)
            {
                int a = attackers, d = defenders;
                while (d > 0 && a > 1)
                {
                    int dice = Math.Min(a - 1, d);
                    for (int i = 0; i < dice; i++)
                    {
                        int x = Random.Shared.Next(1, 7), y = Random.Shared.Next(1, 7);
                        if (x > y) d--; else if (y > x) a--;
                    }
                }
                if (d == 0) wins++;
            }
            return wins / 3000.0;
        }
    }
}
