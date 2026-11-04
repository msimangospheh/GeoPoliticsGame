using GeoPolitics_Game.Identity;
using GeoPolitics_Game.Services.Email;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GeoPolitics_Game.Controllers
{
    // "Forgot password": email a 6-digit one-time code (OTP), then let the player set a new password.
    //
    // Security choices:
    //  - The code comes from ASP.NET Identity's built-in email token provider. It is time-based
    //    (expires in a few minutes) and tied to the user's security stamp, so it stops working
    //    as soon as the password changes. Nothing extra is stored in the database.
    //  - Every wrong code counts as a failed login: 5 wrong codes lock the account for 5 minutes.
    //  - The page never says whether an email address has an account (no "account enumeration").
    //  - Sending codes is rate-limited per IP address (policy "otp" in Program.cs).
    [AllowAnonymous]
    public class PasswordController : Controller
    {
        private const string Purpose = "ResetPassword";
        private const int CodeMinutes = 9;   // Identity's email codes stay valid for roughly 9 minutes

        private readonly UserManager<ApplicationUser> _users;
        private readonly IEmailService _email;
        private readonly ILogger<PasswordController> _logger;

        public PasswordController(UserManager<ApplicationUser> users, IEmailService email,
                                  ILogger<PasswordController> logger)
        {
            _users = users;
            _email = email;
            _logger = logger;
        }

        // GET /Password/Forgot
        [HttpGet]
        public IActionResult Forgot() => View(new ForgotPasswordViewModel());

        // POST /Password/Forgot
        [HttpPost, EnableRateLimiting("otp")]
        public async Task<IActionResult> Forgot(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.FindByEmailAsync(model.Email);
            if (user != null && !await _users.IsLockedOutAsync(user))
            {
                var code = await _users.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, Purpose);
                var (subject, html, text) = EmailTemplates.PasswordResetCode(user.DisplayName, code, CodeMinutes);

                if (await _email.SendAsync(user.Email!, user.DisplayName, subject, html, text))
                    _logger.LogInformation("Password reset code sent to user {UserId}", user.Id);
                else
                    _logger.LogError("Password reset code could NOT be sent to user {UserId}", user.Id);
            }

            // Same message whether or not the account exists
            TempData["Success"] = "If that email belongs to a commander, a 6-digit code is on its way. Check your inbox (and spam folder).";
            return RedirectToAction(nameof(Reset), new { email = model.Email });
        }

        // GET /Password/Reset?email=...
        [HttpGet]
        public IActionResult Reset(string? email) => View(new ResetPasswordViewModel { Email = email ?? string.Empty });

        // POST /Password/Reset
        [HttpPost]
        public async Task<IActionResult> Reset(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            const string invalid = "That code is wrong or has expired. Check the email or request a new code.";
            var user = await _users.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, invalid);
                return View(model);
            }

            if (await _users.IsLockedOutAsync(user))
            {
                ModelState.AddModelError(string.Empty, "Too many wrong codes. Wait 5 minutes, then request a new code.");
                return View(model);
            }

            var codeOk = await _users.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, Purpose, model.Code.Trim());
            if (!codeOk)
            {
                await _users.AccessFailedAsync(user);   // counts toward the 5-attempt lockout
                ModelState.AddModelError(string.Empty, invalid);
                return View(model);
            }

            // The code is right: swap it for Identity's normal reset token and change the password.
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await _users.ResetAccessFailedCountAsync(user);
            _logger.LogInformation("Password reset completed for user {UserId}", user.Id);

            TempData["Success"] = "Password changed. Log in with your new password.";
            return RedirectToAction("Login", "Account");
        }
    }
}
