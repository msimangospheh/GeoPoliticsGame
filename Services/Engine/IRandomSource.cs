using System.Security.Cryptography;

namespace GeoPolitics_Game.Services.Engine
{
    // Anything random in the game goes through this interface.
    // The real game uses secure random numbers; tests use fake dice with known results.
    public interface IRandomSource
    {
        int RollDie();               // 1 to 6
        int Next(int maxExclusive);  // 0 to maxExclusive - 1
    }

    public class SecureRandomSource : IRandomSource
    {
        // Cryptographic randomness: players can't predict the server's dice
        public int RollDie() => RandomNumberGenerator.GetInt32(1, 7);
        public int Next(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
    }
}
