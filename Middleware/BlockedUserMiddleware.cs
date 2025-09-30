using Microsoft.AspNetCore.Identity;
using MunicipalReportsAPI.Models;
using System.Security.Claims;

namespace MunicipalReportsAPI.Middleware
{
    public class BlockedUserMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<BlockedUserMiddleware> _logger;

        public BlockedUserMiddleware(RequestDelegate next, ILogger<BlockedUserMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            // Skip check for anonymous requests or specific endpoints
            if (!context.User.Identity?.IsAuthenticated == true ||
                IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Get user ID from claims
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var user = await userManager.FindByIdAsync(userId);
                    if (user != null && user.IsBlocked)
                    {
                        _logger.LogWarning("Blocked user {UserId} attempted to access {Path}", userId, context.Request.Path);

                        context.Response.StatusCode = 403; // Forbidden
                        context.Response.ContentType = "application/json";

                        var response = new
                        {
                            success = false,
                            message = "Your account has been blocked. Please contact support.",
                            error = "ACCOUNT_BLOCKED"
                        };

                        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking blocked status for user {UserId}", userId);
                    // Continue with the request if there's an error checking the user
                }
            }

            await _next(context);
        }

        private static bool IsExcludedPath(PathString path)
        {
            // Exclude authentication endpoints and static files
            var excludedPaths = new[]
            {
                "/api/Identity/login",
                "/api/Identity/register",
                "/api/Identity/forgot-password",
                "/api/Identity/reset-password",
                "/api/Identity/confirm-email",
                "/api/Identity/google-login",
                "/swagger",
                "/uploads"
            };

            return excludedPaths.Any(excluded => path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
        }
    }
}