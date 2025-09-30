using MunicipalReportsAPI.Models;

namespace MunicipalReportsAPI.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailConfirmationAsync(ApplicationUser user, string confirmationLink);
        Task<bool> SendPasswordResetAsync(string email, string resetLink);
        Task<bool> SendWelcomeEmailAsync(ApplicationUser user);
    }
}