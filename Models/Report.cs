using MunicipalReportsAPI.Models;
using static MunicipalReportsAPI.Models.Common;
namespace MunicipalReportsAPI.Models
{
    public class Report
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ReportImage { get; set; } = string.Empty;

        // Foreign Keys
        public int CategoryId { get; set; }
        public string? UserId { get; set; } // Nullable for anonymous reports

        // Navigation Properties
        public virtual Category Category { get; set; } = null!;
        public virtual ApplicationUser? User { get; set; } // Nullable

        // Status and Tracking
        public ReportStatus Status { get; set; } = ReportStatus.Submitted;
        public string? AdminNotes { get; set; } // Nullable

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        // Contact info for anonymous reports
        public string? ContactEmail { get; set; } // Nullable
        public string? ContactPhone { get; set; } // Nullable
    }

}