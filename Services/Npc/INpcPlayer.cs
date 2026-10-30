using GeoPolitics_Game.Models;
using GeoPolitics_Game.Services.Engine;

namespace GeoPolitics_Game.Services.Npc
{
    // Anything that can play a turn for an NPC
    public interface INpcPlayer
    {
        List<GameEvent> PlayTurn(Game game);
    }

    // TEMPORARY placeholder - Phase 6 replaces this with the real NPC brain.
    // It places reinforcements on its strongest country, then passes without attacking.
    public class PassiveNpcPlayer : INpcPlayer
    {
        private readonly IGameEngine _engine;

        public PassiveNpcPlayer(IGameEngine engine) => _engine = engine;

        public List<GameEvent> PlayTurn(Game game)
        {
            var id = game.CurrentPlayerId;
            var player = game.GetPlayer(id);
            var events = new List<GameEvent>();

            if (game.Phase == TurnPhase.Reinforce)
            {
                var strongest = game.TerritoriesOf(id).OrderByDescending(t => t.Soldiers).First();
                events.AddRange(_engine.PlaceReinforcements(game, id,
                    new Dictionary<string, int> { [strongest.CountryCode] = player.ReinforcementsToPlace }));
            }

            events.AddRange(_engine.EndTurn(game, id));
            return events;
        }
    }
}