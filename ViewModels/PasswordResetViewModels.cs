using System.ComponentModel.DataAnnotations;

namespace GeoPolitics_Game.ViewModels
{
    // Step 1: "Forgot password" form - which email should get the code?
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    // Step 2: enter the 6-digit code from the email and choose a new password
    public class ResetPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code from the email.")]
        [Display(Name = "6-digit code")]
        public string Code { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
