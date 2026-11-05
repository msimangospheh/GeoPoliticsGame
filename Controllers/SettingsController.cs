using GeoPolitics_Game.Services.Profile;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    [Authorize]   // only the logged-in player can see or change their own settings
    public class SettingsController : Controller
    {
        private readonly IPlayerProfileService _profiles;

        public SettingsController(IPlayerProfileService profiles) => _profiles = profiles;

        // GET /Settings
        public async Task<IActionResult> Index() => View(await BuildAsync());

        // POST /Settings/Profile
        [HttpPost]
        public async Task<IActionResult> Profile([Bind(Prefix = "Profile")] ProfileForm form)
        {
            if (!ModelState.IsValid) return View("Index", await BuildAsync(profile: form));

            var result = await _profiles.RenameAsync(User, form.DisplayName);
            if (!result.Succeeded)
            {
                AddErrors(result, "Profile");
                return View("Index", await BuildAsync(profile: form));
            }

            TempData["Success"] = $"You are now Commander {form.DisplayName.Trim()}.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/Preferences
        [HttpPost]
        public async Task<IActionResult> Preferences([Bind(Prefix = "Preferences")] PreferencesForm form)
        {
            // Never trust the browser: check the choices against the real lists
            if (!Enum.IsDefined(form.DefaultDifficulty))
                ModelState.AddModelError("Preferences.DefaultDifficulty", "Choose a difficulty.");
            if (!ChroniclerVoices.All.Contains(form.DefaultVoice))
                ModelState.AddModelError("Preferences.DefaultVoice", "Choose a chronicler voice.");

            if (!ModelState.IsValid) return View("Index", await BuildAsync(preferences: form));

            await _profiles.SavePreferencesAsync(User, form.ToPreferences());
            TempData["Success"] = "Preferences saved.";
            TempData["ApplySound"] = true;
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/Password
        [HttpPost]
        public async Task<IActionResult> Password([Bind(Prefix = "Password")] ChangePasswordForm form)
        {
            if (!ModelState.IsValid) return View("Index", await BuildAsync());

            var result = await _profiles.ChangePasswordAsync(User, form.CurrentPassword, form.NewPassword);
            if (!result.Succeeded)
            {
                AddErrors(result, "Password");
                return View("Index", await BuildAsync());
            }

            TempData["Success"] = "Password changed.";
            return RedirectToAction(nameof(Index));
        }

        // ---- helpers ----

        // Fill the page from the database, keeping whatever the player just typed into a form with errors
        private async Task<SettingsViewModel> BuildAsync(ProfileForm? profile = null, PreferencesForm? preferences = null)
        {
            var user = await _profiles.GetUserAsync(User) ?? throw new UnauthorizedAccessException();
            return new SettingsViewModel
            {
                Email = user.Email ?? string.Empty,
                Profile = profile ?? new ProfileForm { DisplayName = user.DisplayName },
                Preferences = preferences ?? PreferencesForm.From(user.Preferences ?? new()),
                Password = new ChangePasswordForm(),   // never send passwords back to the page
                ApplySoundNow = TempData["ApplySound"] is true
            };
        }

        private void AddErrors(IdentityResult result, string prefix)
        {
            foreach (var error in result.Errors)
            {
                // Identity says "Incorrect password." for a wrong current password
                var key = error.Code == "PasswordMismatch" ? $"{prefix}.CurrentPassword" : prefix;
                ModelState.AddModelError(key, error.Description);
            }
        }
    }
}
