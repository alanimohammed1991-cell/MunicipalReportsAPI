using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MunicipalReportsAPI.Data;
using MunicipalReportsAPI.Models;
using MunicipalReportsAPI.Services;
using MunicipalReportsAPI.Middleware;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("No database connection string found. Set either ConnectionStrings__DefaultConnection or DATABASE_URL");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Stronger password policy
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 4;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;

    // Lockout settings for security
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (!string.IsNullOrEmpty(jwtSecretKey) && !string.IsNullOrEmpty(jwtIssuer) && !string.IsNullOrEmpty(jwtAudience))
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["GoogleOAuth:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["GoogleOAuth:ClientSecret"] ?? "";
    });
}
else
{
    builder.Services.AddAuthentication();
    Console.WriteLine("Warning: JWT configuration is missing. Using default authentication.");
}

// Add services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure file upload options
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();

// Add Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Municipal Reports API",
        Version = "v1",
        Description = "API for citizen reporting system"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("VueJSApp", policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://localhost:3000", "http://localhost:3001", "http://localhost:3002")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Run database migrations automatically on startup (Production)
if (app.Environment.IsProduction())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            Console.WriteLine("Running database migrations...");
            context.Database.Migrate();
            Console.WriteLine("Database migrations completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
            throw;
        }
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Municipal Reports API V1");
        c.RoutePrefix = string.Empty; // Makes Swagger available at the app's root
    });
}
else
{
    app.UseHsts();
}

// Add global exception handling middleware (MUST be first)
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("VueJSApp");
app.UseHttpsRedirection();

// Static files configuration - IMPORTANT ORDER
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = false,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx =>
    {
        // Add security headers for uploaded files
        ctx.Context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        ctx.Context.Response.Headers.Add("X-Frame-Options", "DENY");

        // Cache uploaded images for 1 day
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
        {
            ctx.Context.Response.Headers.Add("Cache-Control", "public,max-age=86400");
        }
    }
});

app.UseAuthentication();
app.UseMiddleware<BlockedUserMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Ensure uploads directory exists
var webRootPath = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(webRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.Run();