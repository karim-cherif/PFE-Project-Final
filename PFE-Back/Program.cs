using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PFE_Back.Hubs;
using PFE_Back.services;
using PFE_Back.Middleware;
using System.Text;
using Hangfire;
using PFE_Back.data;
using PFE_Back.interfaces;
using PFE_Back.models;
using Microsoft.Extensions.FileProviders;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Swashbuckle.AspNetCore.SwaggerUI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Ensure we're using the correct content root
var currentDir = Directory.GetCurrentDirectory();
var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
var assemblyDir = Path.GetDirectoryName(assemblyLocation);

Console.WriteLine($"🔍 Current directory: {currentDir}");
Console.WriteLine($"🔍 Assembly location: {assemblyLocation}");
Console.WriteLine($"🔍 Assembly directory: {assemblyDir}");

// Find the project root directory containing appsettings.json
string? projectRoot = null;

// Check if appsettings.json is in current directory
if (File.Exists(Path.Combine(currentDir, "appsettings.json")))
{
    projectRoot = currentDir;
    Console.WriteLine($"✅ Found appsettings.json in current directory: {projectRoot}");
}
// Check if we're in a subdirectory and appsettings.json is in PFE-Back subdirectory
else if (Directory.Exists(Path.Combine(currentDir, "PFE-Back")) &&
         File.Exists(Path.Combine(currentDir, "PFE-Back", "appsettings.json")))
{
    projectRoot = Path.Combine(currentDir, "PFE-Back");
    Console.WriteLine($"✅ Found appsettings.json in PFE-Back subdirectory: {projectRoot}");
}
// Check if we're running from bin directory, go up to project root
else if (assemblyDir != null && (assemblyDir.Contains("bin\\Debug") || assemblyDir.Contains("bin/Debug")))
{
    var potentialRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDir)));
    if (potentialRoot != null && File.Exists(Path.Combine(potentialRoot, "appsettings.json")))
    {
        projectRoot = potentialRoot;
        Console.WriteLine($"✅ Found appsettings.json in project root: {projectRoot}");
    }
}

if (projectRoot == null)
{
    throw new FileNotFoundException("Could not locate appsettings.json file. Please ensure the application is running from the correct directory.");
}

// Set the content root and change working directory
Directory.SetCurrentDirectory(projectRoot);
builder.Configuration.SetBasePath(projectRoot);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Add environment-specific configuration
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (environment == "Development" && OperatingSystem.IsMacOS())
{
    builder.Configuration.AddJsonFile("appsettings.mac.json", optional: true, reloadOnChange: true);
}

// Configure Entity Framework with proper connection string handling
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"🔍 Connection string found: {!string.IsNullOrEmpty(connectionString)}");
if (!string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine($"✅ Using connection string: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    Console.WriteLine("❌ No connection string found! Available configuration keys:");
    foreach (var kvp in builder.Configuration.AsEnumerable())
    {
        Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
    }
    throw new InvalidOperationException("DefaultConnection connection string is required but not found in configuration.");
}

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"])),
        ClockSkew = TimeSpan.Zero // Remove default 5 minutes tolerance
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: {Error}", context.Exception.Message);

            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers["Token-Expired"] = "true";
                logger.LogWarning("Token expired");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

            // Log all claims for debugging
            if (context.Principal?.Claims != null)
            {
                logger.LogInformation("Token contains {Count} claims", context.Principal.Claims.Count());
                foreach (var claim in context.Principal.Claims)
                {
                    logger.LogInformation("Found claim: {Type} = {Value}", claim.Type, claim.Value);
                }
            }

            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier) ?? context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub);
            if (userIdClaim != null)
            {
                logger.LogInformation("Token successfully validated for user {UserId}", userIdClaim.Value);
            }
            else
            {
                logger.LogWarning("Token validation succeeded but no user ID claim found. Available claims: {Claims}",
                    string.Join(", ", context.Principal?.Claims.Select(c => $"{c.Type}={c.Value}") ?? Array.Empty<string>()));
                context.Fail("No user ID claim found in token");
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication challenge issued: {Error}", context.Error);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 102400; // 100 KB
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Configure Hangfire with proper connection string handling
var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(hangfireConnectionString))
{
    try
    {
        builder.Services.AddHangfire(x => x.UseSqlServerStorage(hangfireConnectionString));
        builder.Services.AddHangfireServer();
        Console.WriteLine("✅ Hangfire configured with SQL Server storage");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Warning: Hangfire configuration failed: {ex.Message}");
        // Continue without Hangfire if it fails
    }
}
else
{
    Console.WriteLine("⚠️ Warning: No connection string found for Hangfire, skipping Hangfire configuration");
}

// Add caching services
builder.Services.AddMemoryCache();
builder.Services.Configure<PFE_Back.services.CacheOptions>(builder.Configuration.GetSection("Cache"));
builder.Services.AddScoped<PFE_Back.services.CacheOptions>(provider =>
    provider.GetRequiredService<IOptions<PFE_Back.services.CacheOptions>>().Value);
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

// Add security services
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

// Add business services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IOcrStatisticsService, OcrStatisticsService>();
builder.Services.AddHttpClient<IOcrService, OcrService>();
builder.Services.AddHttpClient<ILlmService, LlmService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddLogging();

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            // Get allowed origins from configuration with fallback to development URLs
            var allowedOrigins = new List<string>
            {
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:80",
                "http://localhost:8080",
                "http://localhost",
                "http://frontend:80",
                "https://accounts.google.com",
                "https://accounts.google.com/gsi"
            };

            // Add configured frontend URL if available
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
            if (!string.IsNullOrEmpty(frontendUrl))
            {
                allowedOrigins.Add(frontendUrl);
            }

            builder.WithOrigins(allowedOrigins.ToArray())
                   .AllowAnyMethod()
                   .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "Cache-Control", "Accept", "Origin", "X-Custom-Header")
                   .AllowCredentials()
                   .SetIsOriginAllowed(origin =>
                   {
                       // Allow Google OAuth domains
                       if (origin?.Contains("google.com") == true ||
                           origin?.Contains("googleapis.com") == true ||
                           origin?.Contains("gstatic.com") == true)
                       {
                           return true;
                       }
                       return allowedOrigins.Contains(origin);
                   });
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DocumentOcrAI API", Version = "v1" });

    // Add custom schema ID generator
    c.CustomSchemaIds(type =>
    {
        if (type.FullName.Contains("DTOs"))
            return type.Name + "Dto";
        return type.Name;
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            new string[] { }
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocumentOcrAI API v1");
        c.RoutePrefix = "docs";
    });
}

app.UseHttpsRedirection();

// Handle preflight requests for Google OAuth and document downloads
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With, Cache-Control, Accept";
        context.Response.Headers["Access-Control-Max-Age"] = "86400";
        context.Response.StatusCode = 200;
        return;
    }
    await next();
});

app.UseCors("AllowFrontend");

// Add security headers (Google OAuth compatible)
app.Use(async (context, next) =>
{
    // Remove COOP and COEP headers that interfere with Google OAuth
    // Google OAuth requires relaxed policies for popup authentication
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Explicitly set CORS headers for Google OAuth and document downloads
    var origin = context.Request.Headers["Origin"].ToString();
    if (!string.IsNullOrEmpty(origin) && (
        origin.Contains("localhost") ||
        origin.Contains("google.com") ||
        origin.Contains("googleapis.com")))
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With, Cache-Control, Accept";
    }

    await next();
});

// Create uploads directory if it doesn't exist
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Add security middleware early in pipeline
app.UseGlobalExceptionMiddleware();
app.UseSecurityMiddleware();

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
    RequestPath = "/uploads"
});
app.UseAuthentication();
app.UseAuthorization();

// Only use Hangfire dashboard if properly configured
try
{
    app.UseHangfireDashboard();
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Hangfire dashboard not available: {ex.Message}");
}

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "PFE-Back API", timestamp = DateTime.UtcNow }));

app.MapControllers();

// Configure SignalR endpoint with WebSocket transport
app.MapHub<NotificationHub>("/notificationHub", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
    options.CloseOnAuthenticationExpiration = true;
    options.ApplicationMaxBufferSize = 1024 * 1024; // 1MB
    options.TransportMaxBufferSize = 1024 * 1024; // 1MB
});

// Add CORS policy for SignalR
var signalROrigins = new List<string>
{
    "http://localhost:5173",
    "http://localhost:5174",
    "http://localhost:80",
    "http://localhost:8080",
    "http://localhost",
    "http://frontend:80"
};

var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
if (!string.IsNullOrEmpty(frontendUrl))
{
    signalROrigins.Add(frontendUrl);
}

app.UseCors(builder => builder
    .WithOrigins(signalROrigins.ToArray())
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

app.Run();