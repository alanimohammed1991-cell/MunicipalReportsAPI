using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MunicipalReportsAPI.Data;
using MunicipalReportsAPI.Models;
using System.Security.Claims;
using static MunicipalReportsAPI.Models.Common;

namespace MunicipalReportsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _context;
        public ReportsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Report>>> GetReports()
        {
            return await _context.Reports.ToListAsync();
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetMyReports()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reports = await _context.Reports
                .Include(r => r.Category)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Address,
                    r.ReportImage,
                    categoryId = r.CategoryId,
                    categoryName = r.Category.Name,
                    categoryIcon = r.Category.Icon,
                    categoryColor = r.Category.Color,
                    r.Status,
                    r.AdminNotes,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.ResolvedAt,
                    r.ContactEmail,
                    r.ContactPhone,
                    hasImage = !string.IsNullOrEmpty(r.ReportImage)
                })
                .ToListAsync();

            return Ok(new { success = true, reports });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Report>> GetReport(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
            {
                return NotFound();
            }
            return report;
        }
        [HttpPost]
        public async Task<ActionResult<Report>> PostReport(Report report)
        {
            // Get user ID if authenticated (null if anonymous)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            report.UserId = userId;
            report.CreatedAt = DateTime.UtcNow;

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetReport", new { id = report.Id }, report);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutReport(int id, Report report)
        {
            if (id != report.Id)
            {
                return BadRequest();
            }
            _context.Entry(report).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReport(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
            {
                return NotFound();
            }
            _context.Reports.Remove(report);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("with-image")]
        public async Task<IActionResult> CreateReportWithImage(
            [FromForm] string title,
            [FromForm] string description,
            [FromForm] string address,
            [FromForm] int categoryId,
            [FromForm] string? contactEmail,
            [FromForm] string? contactPhone,
            [FromForm] IFormFile? image)
        {
            // Validate category exists
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryId);
            if (!categoryExists)
            {
                return BadRequest(new { success = false, message = "Invalid category ID" });
            }

            // Get user ID if authenticated (null if anonymous)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var report = new Report
            {
                Title = title,
                Description = description,
                Address = address,
                CategoryId = categoryId,
                ContactEmail = contactEmail,
                ContactPhone = contactPhone,
                UserId = userId, // Assign user ID if authenticated
                CreatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            // Upload image if provided
            if (image != null && image.Length > 0)
            {
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
                if (allowedTypes.Contains(image.ContentType.ToLower()) && image.Length <= 5 * 1024 * 1024)
                {
                    var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    var fileName = $"{report.Id}_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    report.ReportImage = $"/uploads/{fileName}";
                    await _context.SaveChangesAsync();
                }
            }

            return CreatedAtAction(nameof(GetReport), new { id = report.Id },
                new { success = true, reportId = report.Id, message = "Report created successfully" });
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchReports(
    string? keyword,
    int? categoryId,
    ReportStatus? status,
    DateTime? fromDate,
    DateTime? toDate,
    string? address,
    bool? hasImage,
    bool? isAnonymous,
    string? sortBy = "created", // created, title, status, category
    string? sortOrder = "desc", // asc, desc
    int page = 1,
    int pageSize = 20)
        {
            var query = _context.Reports
                .Include(r => r.Category)
                .Include(r => r.User)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(r => r.Title.Contains(keyword) ||
                                        r.Description.Contains(keyword) ||
                                        r.Address.Contains(keyword));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(r => r.CategoryId == categoryId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(address))
            {
                query = query.Where(r => r.Address.Contains(address));
            }

            if (hasImage.HasValue)
            {
                if (hasImage.Value)
                {
                    query = query.Where(r => !string.IsNullOrEmpty(r.ReportImage));
                }
                else
                {
                    query = query.Where(r => string.IsNullOrEmpty(r.ReportImage));
                }
            }

            if (isAnonymous.HasValue)
            {
                if (isAnonymous.Value)
                {
                    query = query.Where(r => r.UserId == null);
                }
                else
                {
                    query = query.Where(r => r.UserId != null);
                }
            }

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "title" => sortOrder == "asc" ? query.OrderBy(r => r.Title) : query.OrderByDescending(r => r.Title),
                "status" => sortOrder == "asc" ? query.OrderBy(r => r.Status) : query.OrderByDescending(r => r.Status),
                "category" => sortOrder == "asc" ? query.OrderBy(r => r.Category.Name) : query.OrderByDescending(r => r.Category.Name),
                "address" => sortOrder == "asc" ? query.OrderBy(r => r.Address) : query.OrderByDescending(r => r.Address),
                _ => sortOrder == "asc" ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var reports = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Address,
                    r.ReportImage,
                    categoryId = r.CategoryId,
                    categoryName = r.Category.Name,
                    categoryIcon = r.Category.Icon,
                    categoryColor = r.Category.Color,
                    userId = r.UserId,
                    userName = r.User != null ? r.User.FullName ?? r.User.UserName : "Anonymous",
                    isAnonymous = r.UserId == null,
                    r.Status,
                    r.AdminNotes,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.ResolvedAt,
                    r.ContactEmail,
                    r.ContactPhone,
                    hasImage = !string.IsNullOrEmpty(r.ReportImage),
                    daysSinceCreated = EF.Functions.DateDiffDay(r.CreatedAt, DateTime.Now)
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = reports,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages,
                    hasNext = page < totalPages,
                    hasPrevious = page > 1
                },
                filters = new
                {
                    keyword,
                    categoryId,
                    status,
                    fromDate,
                    toDate,
                    address,
                    hasImage,
                    isAnonymous,
                    sortBy,
                    sortOrder
                }
            });
        }

        [HttpGet("filters")]
        public async Task<IActionResult> GetFilterOptions()
        {
            var categories = await _context.Categories
                .Select(c => new { c.Id, c.Name, c.Icon, c.Color })
                .ToListAsync();

            var statusOptions = Enum.GetValues<ReportStatus>()
                .Select(s => new {
                    value = (int)s,
                    name = s.ToString(),
                    displayName = s.ToString().Replace("_", " ")
                })
                .ToList();

            return Ok(new
            {
                success = true,
                categories,
                statusOptions,
                sortOptions = new[]
                {
            new { value = "created", name = "Created Date" },
            new { value = "title", name = "Title" },
            new { value = "status", name = "Status" },
            new { value = "category", name = "Category" },
            new { value = "address", name = "Address" }
        },
                sortOrderOptions = new[]
                {
            new { value = "desc", name = "Descending" },
            new { value = "asc", name = "Ascending" }
        }
            });
        }

    }
}
