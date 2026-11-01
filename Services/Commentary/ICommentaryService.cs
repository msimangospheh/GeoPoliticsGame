namespace GeoPolitics_Game.Services.Commentary
{
    // The facts about one dice roll that the Chronicler is allowed to talk about
    public record RollFacts(
        string Attacker, string Defender,              // country names
        string AttackerOwner, string DefenderOwner,    // "our commander" or "The Aggressor (Chile)"
        IReadOnlyList<int> AttackerDice, IReadOnlyList<int> DefenderDice,
        int AttackerLosses, int DefenderLosses,
        int AttackerLeft, int DefenderLeft,
        string Outcome);                               // "continues", "conquered", "failed" or "retreat"

    // Anything that can commentate on a battle
    public interface ICommentaryService
    {
        // Returns exactly one line per roll, in order. Never throws.
        Task<List<string>> CommentateAsync(string voice, IReadOnlyList<RollFacts> rolls, CancellationToken ct = default);
    }

    // Fallback lines used when the AI is switched off, has no key, or fails
    public static class TemplateCommentary
    {
        public static List<string> Lines(string voice, IReadOnlyList<RollFacts> rolls) =>
            rolls.Select(r => Line(voice, r)).ToList();

        public static string Line(string voice, RollFacts r)
        {
            string what = r.Outcome switch
            {
                "conquered" => $"{r.Defender} falls to {r.Attacker}!",
                "failed" => $"{r.Attacker}'s assault collapses with one soldier left standing.",
                "retreat" => $"{r.Attacker} sounds the retreat.",
                _ when r.DefenderLosses > r.AttackerLosses => $"{r.Attacker} breaks through and {r.Defender} loses {r.DefenderLosses}.",
                _ when r.AttackerLosses > r.DefenderLosses => $"{r.Defender} holds firm and {r.Attacker} loses {r.AttackerLosses}.",
                _ => $"Blow for blow between {r.Attacker} and {r.Defender}."
            };

            return voice switch
            {
                "War poet" => $"Steel meets steel: {what}",
                "Dry historian" => $"The records note that {char.ToLower(what[0])}{what[1..]}",
                _ => $"What a roll! {what}"
            };
        }
    }
}