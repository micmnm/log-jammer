using Fido2NetLib;
using LogJammer.Api.Auth;
using LogJammer.Api.BackgroundServices;
using LogJammer.Engine;
using LogJammer.Engine.Data;
using LogJammer.Engine.Drain;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Database
builder.Services.AddDbContext<LogJammerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddSingleton<SetupService>();

// Fido2
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"]!;
    options.ServerName = builder.Configuration["Fido2:ServerName"]!;
    options.Origins = builder.Configuration.GetSection("Fido2:Origins").Get<HashSet<string>>()!;
});

// Session (needed for WebAuthn challenge storage)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Engine
builder.Services.AddSingleton(new DrainConfig());
builder.Services.AddSingleton<IngestionPipeline>();
builder.Services.AddScoped<BaselineCalculator>();
builder.Services.AddScoped<PatternStore>();

// Background services
builder.Services.AddHostedService<BaselineRecalculationService>();
builder.Services.AddHostedService<DataRetentionService>();
builder.Services.AddHostedService<ElasticsearchPollingService>();

// API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    options.AddPolicy("ExtensionCors", policy =>
        policy.SetIsOriginAllowed(origin => origin.StartsWith("chrome-extension://"))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
    await db.Database.MigrateAsync();
}

// Bootstrap admin setup
var setupService = app.Services.GetRequiredService<SetupService>();
var urls = app.Urls.Any() ? app.Urls.ToArray() : ["http://localhost:5050"];
await setupService.CheckHttpsAsync(urls);
await setupService.CheckAndBootstrapAsync(urls.First());

// Short-circuit health check before logging/routing middleware to reduce log noise
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("ok");
        return;
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors("DevCors");
}
else
{
    app.UseCors("ExtensionCors");
}

app.UseSession();
app.UseMiddleware<AuthMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
