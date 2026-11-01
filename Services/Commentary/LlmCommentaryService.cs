using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace GeoPolitics_Game.Services.Commentary
{
    // Values from appsettings.json "Llm" + the secret Llm:ApiKey
    public class LlmSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
        public string Model { get; set; } = "openai/gpt-oss-20b";
        public bool Enabled { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 8;
    }

    // Works with ANY "OpenAI-compatible" chat API: Groq, Google Gemini, Mistral, OpenRouter...
    // Falls back to template lines on ANY problem, so the game never breaks.
    public class LlmCommentaryService : ICommentaryService
    {
        private readonly HttpClient _http;
        private readonly LlmSettings _settings;
        private readonly ILogger<LlmCommentaryService> _logger;

        public LlmCommentaryService(HttpClient http, IOptions<LlmSettings> options, ILogger<LlmCommentaryService> logger)
        {
            _http = http;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<List<string>> CommentateAsync(string voice, IReadOnlyList<RollFacts> rolls, CancellationToken ct = default)
        {
            var fallback = TemplateCommentary.Lines(voice, rolls);
            if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey) || rolls.Count == 0)
                return fallback;

            try
            {
                // Never keep the player waiting long: give up after a few seconds
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

                var body = new
                {
                    model = _settings.Model,
                    max_tokens = Math.Min(4000, 300 * rolls.Count + 600),
                    messages = new object[]
                    {
                        new { role = "system", content = SystemPrompt(voice) },
                        new { role = "user", content = UserPrompt(rolls) }
                    }
                };

                var url = _settings.BaseUrl.TrimEnd('/') + "/chat/completions";
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);   // key stays on the server

                using var response = await _http.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Commentary API returned {Status}; using template lines.", (int)response.StatusCode);
                    return fallback;
                }

                using var doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);

                // OpenAI-style reply: choices[0].message.content
                var text = doc.RootElement.GetProperty("choices")[0]
                                          .GetProperty("message").GetProperty("content").GetString() ?? "";

                var lines = ParseLines(text, rolls.Count);
                if (lines == null) return fallback;

                // If the AI gave too few lines, fill the gaps from the templates
                return Enumerable.Range(0, rolls.Count)
                                 .Select(i => i < lines.Count ? lines[i] : fallback[i])
                                 .ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                          or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogWarning(ex, "Commentary failed; using template lines.");
                return fallback;
            }
        }

        // Pulls a JSON array of strings out of the AI's reply. Returns null if it can't.
        public static List<string>? ParseLines(string text, int expected)
        {
            int start = text.IndexOf('['), end = text.LastIndexOf(']');
            if (start < 0 || end <= start) return null;
            try
            {
                var lines = JsonSerializer.Deserialize<List<string>>(text[start..(end + 1)]);
                if (lines == null || lines.Count == 0) return null;
                return lines.Select(l => l.Trim())
                            .Where(l => l.Length > 0)
                            .Select(l => l.Length > 240 ? l[..240] : l)   // keep lines short
                            .Take(expected)
                            .ToList();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string SystemPrompt(string voice)
        {
            string persona = voice switch
            {
                "War poet" => "a dramatic war poet who uses vivid, slightly old-fashioned imagery",
                "Dry historian" => "a dry, understated historian with a wry sense of humour",
                _ => "an excitable sports broadcaster calling the big match"
            };

            return $"""
                You are the Chronicler, the live commentator for "Geopolitics", a Risk-style dice strategy game.
                Your voice: {persona}.
                Dice are compared in pairs (attacker die 1 vs defender die 1, and so on). The higher die wins the pair,
                the loser loses one soldier, and a tie kills nobody.
                For each roll you are given, write ONE line of commentary (at most 30 words) about what just happened.
                Rules:
                - Use only the facts given. Never invent numbers, countries or events.
                - Mention the countries by name. The human player is "our commander".
                - Present tense, family-friendly, no real-world politics, real leaders or real wars.
                - Reply with ONLY a JSON array of strings, one string per roll, in order. No other text.
                """;
        }

        private static string UserPrompt(IReadOnlyList<RollFacts> rolls) =>
            $"Write exactly {rolls.Count} line(s), one per roll, as a JSON array of strings.\nRolls in order:\n" +
            JsonSerializer.Serialize(rolls.Select((r, i) => new
            {
                roll = i + 1,
                attacker = r.Attacker,
                attackerArmy = r.AttackerOwner,
                defender = r.Defender,
                defenderArmy = r.DefenderOwner,
                attackerDice = r.AttackerDice,
                defenderDice = r.DefenderDice,
                attackerLost = r.AttackerLosses,
                defenderLost = r.DefenderLosses,
                attackerSoldiersLeft = r.AttackerLeft,
                defenderSoldiersLeft = r.DefenderLeft,
                result = r.Outcome
            }));
    }
}