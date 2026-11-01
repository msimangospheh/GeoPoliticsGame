using GeoPolitics_Game.Services.Commentary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GeoPolitics_Game.Tests
{
    public class CommentaryTests
    {
        private static RollFacts Roll(string outcome) =>
            new("Egypt", "India", "our commander", "The Aggressor (India)",
                new[] { 6, 2 }, new[] { 3, 5 }, 1, 1, 6, 7, outcome);

        [Fact]
        public void Reads_a_json_array_from_the_ai_reply()
        {
            var lines = LlmCommentaryService.ParseLines("Here you go: [\"First line\", \"Second line\"]", 2);
            Assert.Equal(new[] { "First line", "Second line" }, lines);
        }

        [Fact]
        public void Junk_reply_returns_null_so_templates_are_used()
        {
            Assert.Null(LlmCommentaryService.ParseLines("Sorry, I can't help with that.", 1));
        }

        [Fact]
        public async Task Without_an_api_key_the_game_still_gets_one_line_per_roll()
        {
            var service = new LlmCommentaryService(new HttpClient(),
                Options.Create(new LlmSettings { ApiKey = "" }),
                NullLogger<LlmCommentaryService>.Instance);

            var lines = await service.CommentateAsync("War poet", new[] { Roll("continues"), Roll("conquered") });

            Assert.Equal(2, lines.Count);
            Assert.Contains("India", lines[1]);
        }
    }
}