namespace GeoPolitics_Game.Models
{
    // The exam's population -> soldiers table
    public static class SoldierTable
    {
        private static readonly (long MaxPopulation, int Soldiers)[] Brackets =
        {
            (5_000_000, 1), (10_000_000, 2), (25_000_000, 3), (50_000_000, 4),
            (75_000_000, 5), (100_000_000, 6), (125_000_000, 7), (150_000_000, 8)
        };

        public static int ForPopulation(long population)
        {
            foreach (var bracket in Brackets)
                if (population <= bracket.MaxPopulation) return bracket.Soldiers;
            return 9; // anything more
        }
    }
}
