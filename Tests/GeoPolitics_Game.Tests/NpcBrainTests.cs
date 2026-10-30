using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services.Engine;
using GeoPolitics_Game.Services.Npc;
using Xunit;

namespace GeoPolitics_Game.Tests
{
    // Dice that repeat a pattern forever (NPC battles can take many rolls)
    public class CyclingRandom : IRandomSource
    {
        private readonly int[] _dice;
        private int _next;
        public CyclingRandom(params int[] dice) => _dice = dice;
        public int RollDie() => _dice[_next++ % _dice.Length];
        public int Next(int maxExclusive) => 0;
    }

    public class NpcBrainTests
    {
        // A launched game where it is an NPC's turn (every NPC is an Aggressor because Next() returns 0)
        private static (GameEngine engine, Game game, PlayerState npc) Setup(params int[] dice)
        {
            var engine = new GameEngine(new CyclingRandom(dice));
            string[] codes = { "ZA", "IN", "VA", "DE", "EG", "NG", "BR", "AU", "CA", "JP" };
            Country C(string code) => new() { Code = code, Name = code, SoldierValue = 4, Population = 1 };

            var game = engine.CreateDraft("u1", "Commander Test", C("ZA"), codes.Skip(1).Select(C).ToList(),
                                          Difficulty.Commander, new GameSettings());
            engine.Launch(game);

            var npc = game.Players.First(p => !p.IsHuman);
            game.CurrentTurnIndex = game.TurnOrder.IndexOf(npc.Id);
            game.Phase = TurnPhase.Main;
            game.AttackUsedThisTurn = false;
            return (engine, game, npc);
        }

        [Fact]
        public void Npc_attacks_an_easy_target_and_explains_why()
        {
            var (engine, game, npc) = Setup(6, 1);   // attacker always rolls 6, defender 1
            foreach (var t in game.Territories) t.Soldiers = 8;
            game.TerritoriesOf(npc.Id)[0].Soldiers = 10;
            game.GetTerritory("ZA").Soldiers = 1;    // the human is wide open

            var events = new NpcBrain(engine).PlayTurn(game);

            var attack = Assert.Single(events, e => e.Type == GameEventType.AttackStarted);
            Assert.Equal("ZA", attack.ToCode);
            Assert.False(string.IsNullOrEmpty(attack.Reason));
            Assert.Equal(2, attack.Metrics!.Count);
            Assert.Contains(events, e => e.Type == GameEventType.Conquered);
        }

        [Fact]
        public void Npc_skips_when_every_target_is_too_strong()
        {
            var (engine, game, npc) = Setup(1, 6);
            foreach (var t in game.Territories) t.Soldiers = 9;
            game.TerritoriesOf(npc.Id)[0].Soldiers = 2;

            var events = new NpcBrain(engine).PlayTurn(game);

            Assert.DoesNotContain(events, e => e.Type == GameEventType.AttackStarted);
            var skip = events.First(e => e.Type == GameEventType.TurnSkipped);
            Assert.False(string.IsNullOrEmpty(skip.Reason));
            Assert.Equal(2, skip.Metrics!.Count);
        }

        [Fact]
        public void Npc_places_every_reinforcement_with_a_reason()
        {
            var (engine, game, npc) = Setup(1, 6);   // if it attacks, it loses and retreats
            foreach (var t in game.Territories) t.Soldiers = 9;
            game.Phase = TurnPhase.Reinforce;
            npc.ReinforcementsToPlace = 4;

            var events = new NpcBrain(engine).PlayTurn(game);

            Assert.Equal(0, npc.ReinforcementsToPlace);
            var placed = Assert.Single(events, e => e.Type == GameEventType.Reinforced);
            Assert.False(string.IsNullOrEmpty(placed.Reason));
            Assert.Equal(2, placed.Metrics!.Count);
        }

        [Theory]
        [InlineData(Difficulty.Recruit, NpcProfile.Defender, 0.80)]
        [InlineData(Difficulty.Commander, NpcProfile.Opportunist, 0.55)]
        [InlineData(Difficulty.Warlord, NpcProfile.Aggressor, 0.30)]
        public void Difficulty_and_profile_set_the_attack_line(Difficulty difficulty, NpcProfile profile, double expected) =>
            Assert.Equal(expected, NpcStyle.For(difficulty, profile).AttackLine, 2);
    }
}