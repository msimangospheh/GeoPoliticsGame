using System.Security.Claims;
using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Security
{
    // Small helpers for "who is logged in?" and "may they play this game?"
    public static class UserExtensions
    {
        public const string DisplayNameClaim = "display_name";

        public static string GetUserId(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        public static string GetDisplayName(this ClaimsPrincipal user) =>
            user.FindFirstValue(DisplayNameClaim) ?? user.Identity?.Name ?? "Commander";

        // ACCESS RULE: anyone may WATCH a game, but only its logged-in owner may PLAY it
        public static bool IsOwnerOf(this ClaimsPrincipal user, Game game) =>
            user.Identity?.IsAuthenticated == true
            && !string.IsNullOrEmpty(game.OwnerUserId)
            && game.OwnerUserId == user.GetUserId();
    }
}