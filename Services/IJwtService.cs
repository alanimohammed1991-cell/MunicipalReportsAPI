using MunicipalReportsAPI.Models;

namespace MunicipalReportsAPI.Services
{
    public interface IJwtService
    {
        Task<string> GenerateTokenAsync(ApplicationUser user);
    }
}
