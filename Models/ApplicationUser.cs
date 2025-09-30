using Microsoft.AspNetCore.Identity;

namespace MunicipalReportsAPI.Models
{
    public class ApplicationUser: IdentityUser
    {
        public string? FullName { get; set; }
        public bool IsAnonymous { get; set; } = false;
        public bool IsBlocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        public List<Report> Reports { get; set; } = new();
        public List<Report> AssignedReports { get; set; } = new();
    }
}
