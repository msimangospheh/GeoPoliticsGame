using System.Security.Claims;
using GeoPolitics_Game.Data;
using GeoPolitics_Game.Identity;
using GeoPolitics_Game.Models;
using GeoPolitics_Game.Security;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace GeoPolitics_Game.Services.Profile
{
    // Everything the Settings page can change about the logged-in player
    public interface IPlayerProfileService
    {
        Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal);
        Task<UserPreferences> GetPreferencesAsync(ClaimsPrincipal principal);
        Task SavePreferencesAsync(ClaimsPrincipal principal, UserPreferences preferences);
        Task<IdentityResult> RenameAsync(ClaimsPrincipal principal, string newName);
        Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal principal, string currentPassword, string newPassword);
    }

    public class PlayerProfileService : IPlayerProfileService
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly MongoContext _mongo;
        private readonly ILogger<PlayerProfileService> _logger;

        public PlayerProfileService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
                                    MongoContext mongo, ILogger<PlayerProfileService> logger)
        {
            _users = users;
            _signIn = signIn;
            _mongo = mongo;
            _logger = logger;
        }

        public Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => _users.GetUserAsync(principal);

        public async Task<UserPreferences> GetPreferencesAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true) return new UserPreferences();
            var user = await _users.GetUserAsync(principal);
            return user?.Preferences ?? new UserPreferences();
        }

        public async Task SavePreferencesAsync(ClaimsPrincipal principal, UserPreferences preferences)
        {
            var user = await _users.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
            user.Preferences = preferences;
            await _users.UpdateAsync(user);
        }

        public async Task<IdentityResult> RenameAsync(ClaimsPrincipal principal, string newName)
        {
            var user = await _users.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
            newName = newName.Trim();

            user.DisplayName = newName;
            var result = await _users.UpdateAsync(user);
            if (!result.Succeeded) return result;

            // The name shown in the header comes from a claim in the login cookie: replace it
            var claims = await _users.GetClaimsAsync(user);
            var old = claims.FirstOrDefault(c => c.Type == UserExtensions.DisplayNameClaim);
            var fresh = new Claim(UserExtensions.DisplayNameClaim, newName);
            result = old == null ? await _users.AddClaimAsync(user, fresh) : await _users.ReplaceClaimAsync(user, old, fresh);
            if (!result.Succeeded) return result;

            // Keep the player's campaigns (and so the leaderboard) showing the new name
            var userId = user.Id.ToString();
            await _mongo.Games.UpdateManyAsync(
                Builders<Game>.Filter.Eq(g => g.OwnerUserId, userId),
                Builders<Game>.Update.Set(g => g.OwnerName, $"Commander {newName}"));

            await _signIn.RefreshSignInAsync(user);   // re-issue the cookie with the new name
            _logger.LogInformation("User {UserId} renamed to {Name}", userId, newName);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal principal, string currentPassword, string newPassword)
        {
            var user = await _users.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
            var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);   // checks the current password
            if (result.Succeeded)
            {
                await _signIn.RefreshSignInAsync(user);   // stay logged in here; other devices are signed out
                _logger.LogInformation("User {UserId} changed their password", user.Id);
            }
            return result;
        }
    }
}
