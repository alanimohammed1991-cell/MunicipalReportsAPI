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

// Add DbContext - Convert Railway DATABASE_URL to Npgsql format
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Set ConnectionStrings__DefaultConnection environment variable.");

Console.WriteLine($"[DEBUG] Raw connection string length: {connectionString.Length}");
Console.WriteLine($"[DEBUG] Raw connection string: {connectionString}");

// Convert postgresql:// URL format to Npgsql connection string format
if (connectionString.StartsWith("postgresql://") || connectionString.StartsWith("postgres://"))
{
    var uri = new Uri(connectionString);
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]}";
    Console.WriteLine($"[DEBUG] Converted to Npgsql format, length: {connectionString.Length}");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

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
    options.SignIn.RequireConfirmedEmail = false;

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
        policy.AllowAnyOrigin()
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
            Console.WriteLine("Checking database state...");

            // Check if tables exist
            var canConnect = context.Database.CanConnect();
            Console.WriteLine($"Can connect to database: {canConnect}");

            // Check if Categories table exists
            bool tablesExist = false;
            try
            {
                tablesExist = context.Categories.Any() || !context.Categories.Any();
                Console.WriteLine("Categories table exists.");
            }
            catch
            {
                Console.WriteLine("Categories table does NOT exist!");
            }

            if (!tablesExist)
            {
                Console.WriteLine("Creating database schema with EnsureCreated...");
                // Drop everything and recreate
                context.Database.EnsureDeleted();
                var created = context.Database.EnsureCreated();
                Console.WriteLine($"Database created: {created}");
            }
            else
            {
                Console.WriteLine("Running migrations...");
                context.Database.Migrate();
                Console.WriteLine("Migrations completed.");
            }

            // Verify seeded data
            var categoryCount = context.Categories.Count();
            Console.WriteLine($"Categories in database: {categoryCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while setting up the database: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Municipal Reports API V1");
    c.RoutePrefix = string.Empty; // Makes Swagger available at the app's root
});

if (!app.Environment.IsDevelopment())
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