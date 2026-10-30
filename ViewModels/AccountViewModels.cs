using System.ComponentModel.DataAnnotations;

namespace GeoPolitics_Game.ViewModels
{
    // What the Sign-up form sends. The attributes are validation rules,
    // checked in the browser AND again on the server.
    public class RegisterViewModel
    {
        [Required, StringLength(20, MinimumLength = 2)]
        [RegularExpression(@"^[A-Za-z0-9 _-]+$", ErrorMessage = "Use letters, numbers, spaces, - or _ only.")]
        [Display(Name = "Commander name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // What the Log-in form sends
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}