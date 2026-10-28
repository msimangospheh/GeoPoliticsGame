namespace GeoPolitics_Game.Models
{
    // Thrown when someone tries something the rules don't allow,
    // e.g. attacking with 1 soldier or playing out of turn.
    // Controllers will catch this and show the message to the player.
    public class GameRuleException : Exception
    {
        public GameRuleException(string message) : base(message) { }
    }
}
