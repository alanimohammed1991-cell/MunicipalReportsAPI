using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MunicipalReportsAPI.Models;
using MunicipalReportsAPI.Services;
using System.ComponentModel.DataAnnotations;
using static MunicipalReportsAPI.Models.Common;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace MunicipalReportsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IdentityController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public IdentityController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IJwtService jwtService,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _jwtService = jwtService;
            _configuration = configuration;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Validate phone number if provided
            if (!string.IsNullOrEmpty(request.PhoneNumber) && !IsValidPhoneNumber(request.PhoneNumber))
            {
                return BadRequest(new { success = false, message = "Invalid phone number format" });
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { success = false, message = "User with this email already exists" });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = DateTime.UtcNow,
            };


            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            // Assign default role
            await _userManager.AddToRoleAsync(user, "Citizen");

            // Auto-confirm email
            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _userManager.ConfirmEmailAsync(user, emailToken);

            // Generate JWT token
            var token = await _jwtService.GenerateTokenAsync(user);

            return Ok(new
            {
                success = true,
                message = "Registration successful. You can now login.",
                token = token,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    phoneNumber = user.PhoneNumber,
                    createdAt = user.CreatedAt,
                    emailConfirmed = true
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Invalid email or password" });
            }

            if (user.IsBlocked)
            {
                return Unauthorized(new { success = false, message = "Your account has been blocked. Please contact support." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized(new { success = false, message = "Invalid email or password" });
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var token = await _jwtService.GenerateTokenAsync(user);
            var userRoles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                success = true,
                message = "Login successful",
                token = token,
                tokenExpires = DateTime.UtcNow.AddHours(_configuration.GetValue<int>("Jwt:TokenExpirationHours", 24)),
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    phoneNumber = user.PhoneNumber,
                    lastLoginAt = user.LastLoginAt,
                    roles = userRoles
                }
            });
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    phoneNumber = user.PhoneNumber,
                    createdAt = user.CreatedAt,
                    lastLoginAt = user.LastLoginAt,
                    roles = userRoles
                }
            });
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            if (!string.IsNullOrEmpty(request.FullName))
                user.FullName = request.FullName;

            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                if (!IsValidPhoneNumber(request.PhoneNumber))
                {
                    return BadRequest(new { success = false, message = "Invalid phone number format" });
                }
                user.PhoneNumber = request.PhoneNumber;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            return Ok(new { success = true, message = "Profile updated successfully" });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            return Ok(new { success = true, message = "Password changed successfully" });
        }

        [HttpPost("assign-role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            var roleExists = await _roleManager.RoleExistsAsync(request.Role);
            if (!roleExists)
            {
                return BadRequest(new { success = false, message = "Role does not exist" });
            }

            var result = await _userManager.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            return Ok(new { success = true, message = $"Role {request.Role} assigned successfully" });
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return BadRequest(new { success = false, message = "Invalid email confirmation request" });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            if (user.EmailConfirmed)
            {
                return Ok(new { success = true, message = "Email already confirmed" });
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Email confirmation failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            // Send welcome email (don't block if email fails)
            try
            {
                await _emailService.SendWelcomeEmailAsync(user);
            }
            catch (Exception emailEx)
            {
                Console.WriteLine($"Failed to send welcome email: {emailEx.Message}");
            }

            return Ok(new { success = true, message = "Email confirmed successfully! Welcome to Municipal Reports System." });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                // Here you would validate the Google token with Google's API
                // For now, we'll create a basic implementation
                // In production, you should verify the token with Google

                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                ApplicationUser user;

                if (existingUser == null)
                {
                    // Create new user from Google account
                    user = new ApplicationUser
                    {
                        UserName = request.Email,
                        Email = request.Email,
                        FullName = request.Name,
                        EmailConfirmed = true, // Google emails are pre-verified
                        CreatedAt = DateTime.UtcNow,
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = string.Join(", ", createResult.Errors.Select(e => e.Description))
                        });
                    }

                    // Assign default role
                    await _userManager.AddToRoleAsync(user, "Citizen");

                    // Send welcome email (don't block login if email fails)
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user);
                    }
                    catch (Exception emailEx)
                    {
                        // Log but don't fail the login
                        Console.WriteLine($"Failed to send welcome email: {emailEx.Message}");
                    }
                }
                else
                {
                    user = existingUser;
                    // Update last login
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }

                var token = await _jwtService.GenerateTokenAsync(user);
                var userRoles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    success = true,
                    message = "Google login successful",
                    token = token,
                    tokenExpires = DateTime.UtcNow.AddHours(_configuration.GetValue<int>("Jwt:TokenExpirationHours", 24)),
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        fullName = user.FullName,
                        phoneNumber = user.PhoneNumber,
                        createdAt = user.CreatedAt,
                        lastLoginAt = user.LastLoginAt,
                        roles = userRoles
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Google login failed", error = ex.Message });
            }
        }

        [HttpPost("resend-confirmation")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            if (user.EmailConfirmed)
            {
                return BadRequest(new { success = false, message = "Email already confirmed" });
            }

            var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var frontendBaseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:3001";
            var confirmationLink = $"{frontendBaseUrl}/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(emailToken)}";

            var emailSent = await _emailService.SendEmailConfirmationAsync(user, confirmationLink!);

            return Ok(new
            {
                success = true,
                message = "Confirmation email sent successfully",
                emailSent = emailSent
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.EmailConfirmed)
            {
                // Don't reveal whether the user exists or not for security reasons
                return Ok(new
                {
                    success = true,
                    message = "If an account with that email exists, a password reset link has been sent."
                });
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var frontendBaseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:3002";
            var resetLink = $"{frontendBaseUrl}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(resetToken)}";

            var emailSent = await _emailService.SendPasswordResetAsync(user.Email, resetLink);

            return Ok(new
            {
                success = true,
                message = "If an account with that email exists, a password reset link has been sent.",
                emailSent = emailSent
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { success = false, message = "Invalid password reset request" });
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return BadRequest(new { success = false, message = "Invalid password reset request" });
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Password reset failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            // Update last login to current time
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return Ok(new { success = true, message = "Password has been reset successfully" });
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin,MunicipalStaff")]
        public async Task<IActionResult> GetUsers()
        {
            var users = _userManager.Users.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                fullName = u.FullName,
                phoneNumber = u.PhoneNumber,
                createdAt = u.CreatedAt,
                lastLoginAt = u.LastLoginAt,
                isBlocked = u.IsBlocked,
                emailConfirmed = u.EmailConfirmed
            }).ToList();

            await Task.CompletedTask;
            return Ok(new { success = true, users = users });
        }

        [HttpPost("admin/users/{userId}/block")]
        [Authorize(Roles = "Admin,MunicipalStaff")]
        public async Task<IActionResult> BlockUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            // Prevent admin from blocking themselves
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (user.Id == currentUserId)
            {
                return BadRequest(new { success = false, message = "Cannot block yourself" });
            }

            user.IsBlocked = true;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            return Ok(new { success = true, message = "User has been blocked successfully" });
        }

        [HttpPost("admin/users/{userId}/unblock")]
        [Authorize(Roles = "Admin,MunicipalStaff")]
        public async Task<IActionResult> UnblockUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            user.IsBlocked = false;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }

            return Ok(new { success = true, message = "User has been unblocked successfully" });
        }

        private static bool IsValidPhoneNumber(string phoneNumber)
        {
            // Iraqi mobile number format: 07XXXXXXXXX (11 digits starting with 07)
            var phoneRegex = new Regex(@"^07\d{9}$");
            return phoneRegex.IsMatch(phoneNumber.Trim());
        }
    }

    // Simple request classes
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AssignRoleRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public string Role { get; set; } = string.Empty;
    }

    public class GoogleLoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string GoogleToken { get; set; } = string.Empty;
    }

    public class ResendConfirmationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public string Token { get; set; } = string.Empty;
        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

}