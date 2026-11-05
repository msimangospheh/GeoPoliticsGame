using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace GeoPolitics_Game.Identity
{
    // A registered player, stored in the "users" collection.
    // The base class already has Email, PasswordHash, lockout fields, etc.
    [CollectionName("users")]
    public class ApplicationUser : MongoIdentityUser<Guid>
    {
        public string DisplayName { get; set; } = string.Empty;   // "Sphe" -> shown as "Commander Sphe"
        public UserPreferences Preferences { get; set; } = new();   // saved settings (Settings page)
    }

    [CollectionName("roles")]
    public class ApplicationRole : MongoIdentityRole<Guid>
    {
    }
}