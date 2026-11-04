using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace GeoPolitics_Game.Services.Email
{
    // Settings from appsettings.json / user-secrets / Render environment variables ("Email" section)
    public class EmailSettings
    {
        public string ApiKey { get; set; } = string.Empty;        // Brevo API key (secret)
        public string SenderEmail { get; set; } = string.Empty;   // the address you verified in Brevo
        public string SenderName { get; set; } = "Geopolitics War Room";
        public string BaseUrl { get; set; } = "https://api.brevo.com/v3/";
    }

    // The rest of the app only knows "send an email" - not which company delivers it
    public interface IEmailService
    {
        Task<bool> SendAsync(string toEmail, string toName, string subject, string html, string text);
    }

    // Sends email through Brevo's HTTPS API.
    // (Render's free plan blocks normal SMTP email ports, so an HTTPS API is used instead.)
    public class BrevoEmailService : IEmailService
    {
        private readonly HttpClient _http;
        private readonly EmailSettings _settings;
        private readonly IHostEnvironment _env;
        private readonly ILogger<BrevoEmailService> _logger;

        public BrevoEmailService(HttpClient http, IOptions<EmailSettings> settings,
                                 IHostEnvironment env, ILogger<BrevoEmailService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _env = env;
            _logger = logger;
            _http.BaseAddress = new Uri(_settings.BaseUrl);
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<bool> SendAsync(string toEmail, string toName, string subject, string html, string text)
        {
            // No key yet? On your own computer, print the email to the console so you can still test.
            if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.SenderEmail))
            {
                if (_env.IsDevelopment())
                {
                    _logger.LogWarning("[DEV EMAIL - not sent] To: {To} | {Subject}\n{Text}", toEmail, subject, text);
                    return true;
                }
                _logger.LogError("Email is not configured (Email:ApiKey / Email:SenderEmail missing)");
                return false;
            }

            var body = new
            {
                sender = new { name = _settings.SenderName, email = _settings.SenderEmail },
                to = new[] { new { email = toEmail, name = toName } },
                subject,
                htmlContent = html,
                textContent = text
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
                {
                    Content = JsonContent.Create(body)
                };
                request.Headers.Add("api-key", _settings.ApiKey);

                using var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode) return true;

                _logger.LogError("Brevo refused the email: {Status} {Reason}",
                                 (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not reach Brevo to send email");
                return false;
            }
        }
    }

    // The wording and look of the emails the game sends
    public static class EmailTemplates
    {
        public static (string Subject, string Html, string Text) PasswordResetCode(string displayName, string code, int minutes)
        {
            var name = HtmlEncoder.Default.Encode(displayName);
            var subject = $"{code} is your Geopolitics reset code";
            var text = $"Commander {displayName},\n\nYour password reset code is: {code}\n" +
                       $"It expires in about {minutes} minutes.\n\n" +
                       "If you did not ask for this, ignore this email - your password stays the same.";
            var html = $@"
<div style=""font-family:Arial,sans-serif;background:#1d150c;padding:32px;color:#f4ece1"">
  <div style=""max-width:460px;margin:auto;background:#2b1f15;border:1px solid #c9a24a;border-radius:14px;padding:28px"">
    <p style=""letter-spacing:3px;font-size:12px;color:#e8cf94;margin:0"">GEOPOLITICS · THE WAR ROOM</p>
    <h1 style=""font-size:22px;margin:12px 0"">Commander {name},</h1>
    <p style=""color:#e7d3ad"">Use this code to set a new password:</p>
    <p style=""font-size:36px;letter-spacing:10px;font-weight:bold;text-align:center;background:#1d150c;border-radius:10px;padding:14px;color:#d4e09a;margin:18px 0"">{code}</p>
    <p style=""color:#e7d3ad;font-size:14px"">It expires in about {minutes} minutes. If you did not ask for this, ignore this email; your password stays the same.</p>
  </div>
</div>";
            return (subject, html, text);
        }
    }
}
