using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services.Engine;
using Xunit;

namespace GeoPolitics_Game.Tests
{
    // Dice that roll exactly what each test says, so results are predictable
    public class FakeRandom : IRandomSource
    {
        private readonly Queue<int> _dice;
        public FakeRandom(params int[] dice) => _dice = new Queue<int>(dice);
        public int RollDie() => _dice.Count > 0 ? _dice.Dequeue() : throw new InvalidOperationException("Test ran out of dice");
        public int Next(int maxExclusive) => 0;
    }

    public static class TestBoard
    {
        private static readonly string[] Codes = { "ZA", "IN", "VA", "DE", "EG", "NG", "BR", "AU", "CA", "JP" };

        private static Country C(string code, int value) =>
            new() { Code = code, Name = code, SoldierValue = value, Population = 1 };

        // A launched game where it is the human's turn in round 1 (human owns ZA)
        public static (GameEngine engine, Game game) NewGame(params int[] dice)
        {
            var engine = new GameEngine(new FakeRandom(dice));
            var rivals = Codes.Skip(1).Select(c => C(c, 3)).ToList();
            var game = engine.CreateDraft("user1", "Commander Test", C("ZA", 5), rivals,
                                          Difficulty.Commander, new GameSettings());
            engine.Launch(game);
            game.CurrentTurnIndex = game.TurnOrder.IndexOf(game.HumanPlayerId);
            game.Phase = TurnPhase.Main;
            return (engine, game);
        }

        // Ends turns (NPCs place reinforcements on their first country) until it's the human's turn again
        public static void PlayRoundUntilHuman(GameEngine engine, Game game)
        {
            engine.EndTurn(game, game.HumanPlayerId);
            while (game.CurrentPlayerId != game.HumanPlayerId)
            {
                var id = game.CurrentPlayerId;
                if (game.Phase == TurnPhase.Reinforce)
                    engine.PlaceReinforcements(game, id, new Dictionary<string, int>
                    {
                        [game.TerritoriesOf(id)[0].CountryCode] = game.GetPlayer(id).ReinforcementsToPlace
                    });
                engine.EndTurn(game, id);
            }
        }
    }

    public class SoldierTableTests
    {
        [Theory]
        [InlineData(506, 1)]               // Vatican City
        [InlineData(5_000_000, 1)]         // exactly 5 million is still 1
        [InlineData(5_000_001, 2)]
        [InlineData(65_453_084, 5)]        // South Africa
        [InlineData(122_427_731, 7)]       // Japan
        [InlineData(1_476_625_576, 9)]     // India
        public void Population_maps_to_exam_table(long population, int expected) =>
            Assert.Equal(expected, SoldierTable.ForPopulation(population));
    }

    public class TerritoryRuleTests
    {
        [Theory]
        [InlineData(9, 4)]   // exam: India 9 -> max(2, 4) = 4
        [InlineData(1, 2)]   // exam: Vatican City 1 -> max(2, 0) = 2
        [InlineData(5, 2)]
        [InlineData(7, 3)]
        public void Conquest_spoils_are_max_of_2_or_half_value(int value, int expected) =>
            Assert.Equal(expected, new Territory { SoldierValue = value }.ConquestSpoils);

        [Theory]
        [InlineData(10, 2, 2)]  // exam example 1
        [InlineData(3, 5, 2)]   // exam example 2
        [InlineData(2, 5, 1)]   // exam example 3
        public void Dice_count_matches_exam_examples(int attackers, int defenders, int expected) =>
            Assert.Equal(expected, new Territory { Soldiers = attackers }.DiceAgainst(new Territory { Soldiers = defenders }));

        [Fact]
        public void Transfer_must_leave_one_soldier_behind()
        {
            var a = new Territory { CountryCode = "A", OwnerPlayerId = "p", Soldiers = 3 };
            var b = new Territory { CountryCode = "B", OwnerPlayerId = "p", Soldiers = 1 };

            Assert.Throws<GameRuleException>(() => a.TransferTo(b, 3));
            a.TransferTo(b, 2);
            Assert.Equal(1, a.Soldiers);
            Assert.Equal(3, b.Soldiers);
        }
    }

    public class BattleTests
    {
        [Fact]
        public void Exam_example_both_sides_lose_one_soldier()
        {
            // Attacker dice 1, 4 vs defender dice 2, 3 (straight from the exam)
            var (engine, game) = TestBoard.NewGame(1, 4, 2, 3);
            var me = game.GetTerritory("ZA"); me.Soldiers = 10;
            var target = game.GetTerritory("IN"); target.Soldiers = 2;

            engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN");
            engine.Roll(game, game.HumanPlayerId);

            Assert.Equal(9, me.Soldiers);
            Assert.Equal(1, target.Soldiers);
            Assert.NotNull(game.ActiveBattle);   // battle continues
        }

        [Fact]
        public void Tie_kills_nobody()
        {
            var (engine, game) = TestBoard.NewGame(3, 3);
            game.GetTerritory("ZA").Soldiers = 2;
            game.GetTerritory("IN").Soldiers = 1;

            engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN");
            engine.Roll(game, game.HumanPlayerId);

            Assert.Equal(2, game.GetTerritory("ZA").Soldiers);
            Assert.Equal(1, game.GetTerritory("IN").Soldiers);
        }

        [Fact]
        public void Conquest_changes_owner_and_pays_spoils()
        {
            var (engine, game) = TestBoard.NewGame(6, 1);
            game.GetTerritory("ZA").Soldiers = 5;
            game.GetTerritory("IN").Soldiers = 1;   // value 3 -> spoils max(2, 1) = 2

            engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN");
            engine.Roll(game, game.HumanPlayerId);

            var india = game.GetTerritory("IN");
            Assert.Equal(game.HumanPlayerId, india.OwnerPlayerId);
            Assert.Equal(2, india.Soldiers);
            Assert.Null(game.ActiveBattle);
        }

        [Fact]
        public void Attack_fails_when_attacker_is_down_to_one()
        {
            var (engine, game) = TestBoard.NewGame(1, 6);
            game.GetTerritory("ZA").Soldiers = 2;
            game.GetTerritory("IN").Soldiers = 3;

            engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN");
            engine.Roll(game, game.HumanPlayerId);

            Assert.Equal(1, game.GetTerritory("ZA").Soldiers);
            Assert.NotEqual(game.HumanPlayerId, game.GetTerritory("IN").OwnerPlayerId);
            Assert.Null(game.ActiveBattle);
        }

        [Fact]
        public void Only_one_attack_per_turn()
        {
            var (engine, game) = TestBoard.NewGame(1, 6);
            game.GetTerritory("ZA").Soldiers = 2;
            engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN");
            engine.Roll(game, game.HumanPlayerId);   // fails

            game.GetTerritory("ZA").Soldiers = 5;
            Assert.Throws<GameRuleException>(() => engine.StartAttack(game, game.HumanPlayerId, "ZA", "VA"));
        }

        [Fact]
        public void Cannot_attack_with_one_soldier()
        {
            var (engine, game) = TestBoard.NewGame();
            game.GetTerritory("ZA").Soldiers = 1;
            Assert.Throws<GameRuleException>(() => engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN"));
        }

        [Fact]
        public void Cannot_play_out_of_turn()
        {
            var (engine, game) = TestBoard.NewGame();
            var someoneElse = game.TurnOrder.First(id => id != game.HumanPlayerId);
            Assert.Throws<GameRuleException>(() => engine.StartAttack(game, someoneElse, "IN", "ZA"));
        }

        [Fact]
        public void Conquering_the_last_country_wins_the_game()
        {
            var (engine, game) = TestBoard.NewGame(6, 1);
            foreach (var t in game.Territories.Where(t => t.CountryCode != "IN"))
                t.OwnerPlayerId = game.HumanPlayerId;
            game.GetTerritory("ZA").Soldiers = 5;
            game.GetTerritory("IN").Soldiers = 1;

            engine.StartAttack(game, game.HumanPlayerId, "ZA", "IN");
            engine.Roll(game, game.HumanPlayerId);

            Assert.Equal(GameStatus.Won, game.Status);
        }
    }

    public class TurnTests
    {
        [Fact]
        public void No_reinforcements_in_round_one()
        {
            var (_, game) = TestBoard.NewGame();
            Assert.Equal(0, game.GetPlayer(game.HumanPlayerId).ReinforcementsToPlace);
        }

        [Fact]
        public void Round_two_gives_two_soldiers_per_country()
        {
            var (engine, game) = TestBoard.NewGame();

            // Give the human 2 more countries; their old owners are eliminated
            foreach (var code in new[] { "IN", "VA" })
            {
                var t = game.GetTerritory(code);
                game.GetPlayer(t.OwnerPlayerId).IsEliminated = true;
                t.OwnerPlayerId = game.HumanPlayerId;
            }

            TestBoard.PlayRoundUntilHuman(engine, game);

            Assert.Equal(2, game.Round);
            Assert.Equal(TurnPhase.Reinforce, game.Phase);
            Assert.Equal(6, game.GetPlayer(game.HumanPlayerId).ReinforcementsToPlace);   // 3 countries x 2
        }
    }

    public class OddsTests
    {
        [Theory]
        [InlineData(2, 1, 0.500)]
        [InlineData(10, 2, 0.989)]
        [InlineData(7, 8, 0.291)]
        public void Win_probability_matches_simulation(int attackers, int defenders, double expected) =>
            Assert.Equal(expected, BattleOdds.WinProbability(attackers, defenders), 3);
    }
}
