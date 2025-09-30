using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalReportsAPI.Data;
using MunicipalReportsAPI.Models;
using static MunicipalReportsAPI.Models.Common;

namespace MunicipalReportsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,MunicipalStaff")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var totalReports = await _context.Reports.CountAsync();
            var newReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.Submitted);
            var inReviewReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.InReview);
            var inProgressReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.InProgress);
            var resolvedReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.Resolved);
            var closedReports = await _context.Reports.CountAsync(r => r.Status == ReportStatus.Closed);

            // This week's reports
            var startOfWeek = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);
            var thisWeekReports = await _context.Reports.CountAsync(r => r.CreatedAt >= startOfWeek);

            // This month's reports
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var thisMonthReports = await _context.Reports.CountAsync(r => r.CreatedAt >= startOfMonth);

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalReports,
                    statusBreakdown = new
                    {
                        submitted = newReports,
                        inReview = inReviewReports,
                        inProgress = inProgressReports,
                        resolved = resolvedReports,
                        closed = closedReports
                    },
                    thisWeekReports,
                    thisMonthReports,
                    completionRate = totalReports > 0 ? Math.Round((double)(resolvedReports + closedReports) / totalReports * 100, 1) : 0
                }
            });
        }

        [HttpGet("category-stats")]
        public async Task<IActionResult> GetCategoryStats()
        {
            var categoryStats = await _context.Reports
                .Include(r => r.Category)
                .GroupBy(r => new { r.Category.Name, r.Category.Color, r.Category.Icon })
                .Select(g => new
                {
                    categoryName = g.Key.Name,
                    categoryColor = g.Key.Color,
                    categoryIcon = g.Key.Icon,
                    count = g.Count(),
                    resolved = g.Count(r => r.Status == ReportStatus.Resolved || r.Status == ReportStatus.Closed),
                    pending = g.Count(r => r.Status == ReportStatus.Submitted || r.Status == ReportStatus.InReview || r.Status == ReportStatus.InProgress)
                })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = categoryStats
            });
        }

        [HttpGet("monthly-trends")]
        public async Task<IActionResult> GetMonthlyTrends(int months = 12)
        {
            var startDate = DateTime.Now.AddMonths(-months);

            var monthlyData = await _context.Reports
                .Where(r => r.CreatedAt >= startDate)
                .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    total = g.Count(),
                    resolved = g.Count(r => r.Status == ReportStatus.Resolved || r.Status == ReportStatus.Closed),
                    pending = g.Count(r => r.Status != ReportStatus.Resolved && r.Status != ReportStatus.Closed)
                })
                .OrderBy(x => x.year)
                .ThenBy(x => x.month)
                .ToListAsync();

            // Fill in missing months with zero data
            var result = new List<object>();
            for (int i = months - 1; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var existing = monthlyData.FirstOrDefault(x => x.year == date.Year && x.month == date.Month);

                result.Add(new
                {
                    year = date.Year,
                    month = date.Month,
                    monthName = date.ToString("MMM yyyy"),
                    total = existing?.total ?? 0,
                    resolved = existing?.resolved ?? 0,
                    pending = existing?.pending ?? 0
                });
            }

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivity(int limit = 20)
        {
            var recentReports = await _context.Reports
                .Include(r => r.Category)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Take(limit)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Status,
                    r.Address,
                    categoryName = r.Category.Name,
                    categoryIcon = r.Category.Icon,
                    categoryColor = r.Category.Color,
                    userName = r.User != null ? r.User.FullName ?? r.User.UserName : "Anonymous",
                    isAnonymous = r.UserId == null,
                    r.CreatedAt,
                    hasImage = !string.IsNullOrEmpty(r.ReportImage),
                    daysSinceCreated = EF.Functions.DateDiffDay(r.CreatedAt, DateTime.Now)
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = recentReports
            });
        }

        [HttpGet("performance-metrics")]
        public async Task<IActionResult> GetPerformanceMetrics()
        {
            var totalReports = await _context.Reports.CountAsync();

            if (totalReports == 0)
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        averageResolutionDays = 0,
                        totalReports = 0,
                        quickResolutions = 0,
                        overdueReports = 0
                    }
                });
            }

            // Calculate average resolution time for resolved reports
            var resolvedReports = await _context.Reports
                .Where(r => r.ResolvedAt.HasValue)
                .Select(r => new
                {
                    ResolutionDays = EF.Functions.DateDiffDay(r.CreatedAt, r.ResolvedAt.Value)
                })
                .ToListAsync();

            var averageResolutionDays = resolvedReports.Any()
                ? Math.Round(resolvedReports.Average(r => r.ResolutionDays), 1)
                : 0;

            // Reports resolved within 7 days
            var quickResolutions = resolvedReports.Count(r => r.ResolutionDays <= 7);

            // Reports older than 30 days and still not resolved
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var overdueReports = await _context.Reports
                .CountAsync(r => r.CreatedAt < thirtyDaysAgo &&
                           r.Status != ReportStatus.Resolved &&
                           r.Status != ReportStatus.Closed);

            return Ok(new
            {
                success = true,
                data = new
                {
                    averageResolutionDays,
                    totalReports,
                    resolvedReports = resolvedReports.Count,
                    quickResolutions,
                    overdueReports,
                    resolutionRate = totalReports > 0 ? Math.Round((double)resolvedReports.Count / totalReports * 100, 1) : 0
                }
            });
        }

        [HttpPut("reports/{id}/status")]
        public async Task<IActionResult> UpdateReportStatus(int id, [FromBody] UpdateReportStatusRequest request)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
            {
                return NotFound(new { success = false, message = "Report not found" });
            }

            // Update status
            report.Status = request.Status;
            report.UpdatedAt = DateTime.UtcNow;

            // Add admin notes if provided
            if (!string.IsNullOrEmpty(request.AdminNotes))
            {
                report.AdminNotes = request.AdminNotes;
            }

            // Set resolved date if status is resolved
            if (request.Status == ReportStatus.Resolved || request.Status == ReportStatus.Closed)
            {
                if (!report.ResolvedAt.HasValue)
                {
                    report.ResolvedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Clear resolved date if status is changed back from resolved
                report.ResolvedAt = null;
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Report status updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Failed to update report status" });
            }
        }
    }

    public class UpdateReportStatusRequest
    {
        public ReportStatus Status { get; set; }
        public string? AdminNotes { get; set; }
    }
}