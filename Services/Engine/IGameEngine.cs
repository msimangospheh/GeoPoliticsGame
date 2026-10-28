using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Services.Engine
{
    // Every action a player (human or NPC) can take.
    // Each method changes the Game and returns the events describing what happened.
    public interface IGameEngine
    {
        // Setup (the New Campaign screen)
        Game CreateDraft(string ownerUserId, string ownerName, Country homeland,
                         IReadOnlyList<Country> rivals, Difficulty difficulty, GameSettings settings);
        void ReplaceRivals(Game game, IReadOnlyList<Country> rivals);   // "Redraw rivals"
        List<GameEvent> Launch(Game game);                              // "Launch campaign"

        // A turn
        List<GameEvent> PlaceReinforcements(Game game, string playerId, IDictionary<string, int> placements);
        List<GameEvent> StartAttack(Game game, string playerId, string fromCode, string toCode);
        List<GameEvent> Roll(Game game, string playerId);
        List<GameEvent> Retreat(Game game, string playerId);
        List<GameEvent> Transfer(Game game, string playerId, string fromCode, string toCode, int amount);
        List<GameEvent> EndTurn(Game game, string playerId);
    }
}
