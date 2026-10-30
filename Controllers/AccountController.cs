using System.Security.Claims;
using GeoPolitics_Game.Identity;
using GeoPolitics_Game.Security;
using GeoPolitics_Game.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GeoPolitics_Game.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly ILogger<AccountController> _logger;

        public AccountController(UserManager<ApplicationUser> users,
                                 SignInManager<ApplicationUser> signIn,
                                 ILogger<AccountController> logger)
        {
            _users = users;
            _signIn = signIn;
            _logger = logger;
        }

        // GET /Account/Register
        [HttpGet, AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        // POST /Account/Register
        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);   // server-side validation

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                DisplayName = model.DisplayName.Trim()
            };

            // CreateAsync HASHES the password - the real password is never stored
            var result = await _users.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _users.AddClaimAsync(user, new Claim(UserExtensions.DisplayNameClaim, user.DisplayName));
                await _signIn.SignInAsync(user, isPersistent: false);
                _logger.LogInformation("New commander registered: {Email}", model.Email);
                return RedirectToAction("Index", "Games");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // GET /Account/Login
        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

        // POST /Account/Login
        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // lockoutOnFailure: 5 wrong passwords locks the account for 5 minutes
            var result = await _signIn.PasswordSignInAsync(model.Email, model.Password,
                                                           model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                // Only go back to pages on OUR site (prevents "open redirect" attacks)
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl);
                return RedirectToAction("Index", "Games");
            }

            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "Too many failed attempts. Try again in 5 minutes."
                : "Incorrect email or password.");   // never say WHICH one was wrong
            return View(model);
        }

        // POST /Account/Logout
        [HttpPost, Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}