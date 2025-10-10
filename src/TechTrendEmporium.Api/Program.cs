using Data;
using External.FakeStore;
using Logica.Interfaces;
using Logica.Repositories;
using Logica.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// === LOAD USER SECRETS IN PRODUCTION FOR LOCAL TESTING ===
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddUserSecrets<Program>();
    Console.WriteLine("[DEBUG] User Secrets loaded for Production environment");
}

// === Resolve connection string according to environment ===
string? connectionString;

if (builder.Environment.IsDevelopment())
{
    // In development, use local connection from appsettings.Development.json
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"[DEVELOPMENT] Using local database: {connectionString}");
}
else
{
    // In production, use Azure connection from multiple sources
    connectionString = 
        builder.Configuration["ConnectionStrings:ProductionConnection"] // User Secrets (local testing)
        ?? builder.Configuration.GetConnectionString("ProductionConnection") // User Secrets (local testing)
        ?? builder.Configuration.GetConnectionString("DefaultConnection") // Azure App Service Connection String
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__ProductionConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? Environment.GetEnvironmentVariable("SQLCONNSTR_ProductionConnection")
        ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
        ?? Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DefaultConnection");

    Console.WriteLine($"[PRODUCTION] Using Azure database");
    Console.WriteLine($"[DEBUG] Connection string found: {!string.IsNullOrEmpty(connectionString)}");
    
    // Additional debug for Azure App Service
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
    {
        Console.WriteLine($"[DEBUG] Running in Azure App Service: {Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")}");
        Console.WriteLine($"[DEBUG] DefaultConnection available: {!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("DefaultConnection"))}");
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Add debugging to see what configuration is available
    Console.WriteLine("[DEBUG] Available configuration keys:");
    foreach (var item in builder.Configuration.AsEnumerable())
    {
        if (item.Key.Contains("Connection", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {item.Key} = {(item.Value?.Length > 0 ? "[SET]" : "[EMPTY]")}");
        }
    }

    throw new InvalidOperationException(
        "Connection string not found. " +
        "Define the appropriate Connection String for the current environment.");
}

// === EF Core (with retries) ===
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// === HttpClient for FakeStore API ===
builder.Services.AddHttpClient<IFakeStoreApiService, FakeStoreApiService>(client =>
{
    var fakeStoreConfig = builder.Configuration.GetSection("FakeStoreApi");
    var baseUrl = fakeStoreConfig["BaseUrl"] ?? "https://fakestoreapi.com";
    var timeoutSeconds = fakeStoreConfig.GetValue<int>("TimeoutSeconds", 30);

    client.BaseAddress = new Uri(baseUrl);
});

// === Dependency Injection ===
// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>(); // registro Wishlist
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IExternalMappingRepository, ExternalMappingRepository>(); // 👈 FALTABA
builder.Services.AddScoped<ICartRepository, CartRepository>();

// Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IWishlistService, WishlistService>(); // servicio Wishlist
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICartService, CartService>();

//  Store / Listing (F01 Product Display Page)
builder.Services.AddScoped<IStoreService, StoreService>();

// Authentication Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// === JWT Authentication Configuration ===
// Get JWT key from multiple locations for Azure compatibility
var jwtKey = configuration["Jwt:Key"] 
          ?? configuration["Jwt_Key"] 
          ?? Environment.GetEnvironmentVariable("Jwt_Key")
          ?? Environment.GetEnvironmentVariable("Jwt__Key");

//Debug aided by AI
if (string.IsNullOrWhiteSpace(jwtKey))
{
    Console.WriteLine("[ERROR] JWT Key not found in any configuration source");
    Console.WriteLine("[DEBUG] Available JWT-related configuration:");
    foreach (var item in configuration.AsEnumerable())
    {
        if (item.Key.Contains("Jwt", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {item.Key} = {(item.Value?.Length > 0 ? "[SET]" : "[EMPTY]")}");
        }
    }
    throw new InvalidOperationException("JWT key was not found in any valid location.");
}

Console.WriteLine($"[DEBUG] JWT Key found: {jwtKey.Length} characters");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// === Standard API services ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// === Swagger configuration with JWT support ===
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TechTrendEmporium.Api", Version = "v1" });
    c.EnableAnnotations();

    // Add security definition for Bearer (JWT)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// === Create/verify database ===
if (builder.Configuration.GetValue<bool>("EF:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Setting up database...");
        
        // For development, using EnsureCreated is simpler
        if (builder.Environment.IsDevelopment())
        {
            logger.LogInformation("Creating/verifying development database...");
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Development database created/verified successfully");
        }
        else
        {
            // In production, use migrations
            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");
        }
        
        // Seed users after database is created/migrated
        await DbSeeder.SeedUsersAsync(context, logger);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while setting up the database");

        if (app.Environment.IsProduction())
        {
            logger.LogCritical("Application stopped due to database setup failure in Production");
            throw;
        }
        else
        {
            logger.LogWarning("Database setup failed in Development. The application will continue but may not function correctly.");
        }
    }
}

var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled",
                      app.Environment.IsDevelopment());

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechTrendEmporium.Api v1");
        if (builder.Configuration.GetValue<bool>("Swagger:ServeAtRoot", false))
            c.RoutePrefix = string.Empty;
    });
}

// *** NO usar app.UseForwardedHeaders(...) si vamos a setear el setting en Azure ***

// Decide si forzar redirección a HTTPS
// Sugerencia: en producción normalmente NO hace falta; Azure ya puede forzar HTTPS.
// Si quieres forzar desde la app, excluimos /health para que el health-check sea 200.
var forceHttpsRedirect = builder.Configuration.GetValue<bool>(
    "Security:ForceHttpsRedirect",
    app.Environment.IsDevelopment()
);

if (forceHttpsRedirect)
{
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/health"),
        sub => sub.UseHttpsRedirection());
}

// === VERY IMPORTANT THE ORDER! ===
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoint rápido (debe devolver 200)
app.MapGet("/", () => Results.Ok("OK"));
app.MapGet("/health", () => Results.Ok("OK"));

app.Run();
