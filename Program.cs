using SunflowerApi.Data;
using SunflowerApi.Endpoints;
using SunflowerApi.Repositories;
using SunflowerApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuestionnaireApi.Endpoints;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NSwag.Generation.Processors.Security;

var builder = WebApplication.CreateBuilder(args);

// ── Connection string ────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

// ── DbContexts (keep as-is until you decide to remove unused ones) ───────────
builder.Services.AddDbContextPool<ChartDbContext>(options =>
    options.UseNpgsql(connStr, npgOptions => npgOptions.UseVector()));
builder.Services.AddDbContextPool<UserEventsDbContext>(options =>
    options.UseNpgsql(connStr));
builder.Services.AddDbContextPool<DbMetadataDbContext>(options =>
    options.UseNpgsql(connStr));
builder.Services.AddDbContextPool<CategoryDbContext>(options =>
    options.UseNpgsql(connStr));
builder.Services.AddDbContextPool<TranslationDbContext>(options =>
    options.UseNpgsql(connStr));
builder.Services.AddDbContextPool<DictionaryDbContext>(options =>
    options.UseNpgsql(connStr));
builder.Services.AddDbContextPool<QuestionnaireDbContext>(options =>
    options.UseNpgsql(connStr));
builder.Services.AddDbContextPool<FeedbackDbContext>(options =>
    options.UseNpgsql(connStr));

// ── Services & Repositories ──────────────────────────────────────────────────
builder.Services.AddScoped<IDynamicQueryService, DynamicQueryService>();
builder.Services.AddScoped<IChartRepository, ChartRepository>();
builder.Services.AddScoped<IChartService, ChartService>();
builder.Services.AddScoped<IUserEventRepository, UserEventRepository>();
builder.Services.AddScoped<IUserEventService, UserEventService>();
builder.Services.AddScoped<IDbMetadataRepository, DbMetadataRepository>();
builder.Services.AddScoped<IDbMetadataService, DbMetadataService>();
builder.Services.AddScoped<IDictionaryRepository, DictionaryRepository>();
builder.Services.AddScoped<IDictionaryService, DictionaryService>();
builder.Services.AddScoped<IQuestionnaireRepository, QuestionnaireRepository>();
builder.Services.AddScoped<IQuestionnaireService, QuestionnaireService>();
builder.Services.AddScoped<IDataRequestRepository, DataRequestRepository>();
builder.Services.AddScoped<IDataRequestService, DataRequestService>();
builder.Services.AddScoped<ISearchRepository, SearchRepository>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();


// ── Clerk JWT authentication ─────────────────────────────────────────────────

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Clerk exposes its JWKS at {Authority}/.well-known/jwks.json
        // The middleware fetches and caches the public keys automatically.
        options.Authority = builder.Configuration["Clerk:Authority"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Clerk:Authority"],
            ValidateAudience = false,
            ValidateLifetime = !builder.Environment.IsDevelopment(),
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
        };

        options.IncludeErrorDetails = builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires a valid JWT unless it explicitly opts out
    // with .AllowAnonymous()
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── Named rate limiter ───────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("data-request", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(10);
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

});

// ── JSON ─────────────────────────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobile", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ── OpenAPI ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "SunflowerAPI";
    config.Version = "v1";

    // ← Add this block
    config.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste your Clerk JWT here (without the Bearer prefix)"
    });

    config.OperationProcessors.Add(
        new AspNetCoreOperationSecurityScopeProcessor("Bearer")
    );
});

builder.Services.AddOutputCache();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddAzureWebAppDiagnostics();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseCors("AllowMobile");
app.UseOutputCache();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ── Auth middleware — ORDER MATTERS ──────────────────────────────────────────
// Authentication must come before Authorization, and both before UseRateLimiter
// so that the user identity is established before any policy is evaluated.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ── Endpoints ────────────────────────────────────────────────────────────────
app.RegisterDataEndpoints();
app.RegisterUserEventEndpoints();
app.RegisterDbMetadataEndpoints();
app.RegisterDictionaryEndpoints();
app.RegisterQuestionEndpoints();
app.RegisterDataRequestEndpoints();
app.RegisterSearchEndpoints();
app.RegisterFeedbackEndpoints();

app.Run();

// dotnet run --urls "http://0.0.0.0:5013"

