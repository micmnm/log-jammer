using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Data.Seeding;
using LogJammer.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddLogJammerDatabase(connectionString);
builder.Services.AddDataSourceAdapters();

// Services
builder.Services.AddScoped<LogJammer.Api.Services.IDataSourceService, LogJammer.Api.Services.DataSourceService>();
builder.Services.AddScoped<LogJammer.Api.Services.IErrorGroupService, LogJammer.Api.Services.ErrorGroupService>();
builder.Services.AddScoped<LogJammer.Api.Services.ITagService, LogJammer.Api.Services.TagService>();
builder.Services.AddScoped<LogJammer.Api.Services.IConfigurationService, LogJammer.Api.Services.ConfigurationService>();
builder.Services.AddScoped<LogJammer.Api.Services.IClassificationQueueService, LogJammer.Api.Services.ClassificationQueueService>();
builder.Services.AddScoped<LogJammer.Api.Services.IAlertService, LogJammer.Api.Services.AlertService>();
builder.Services.AddScoped<LogJammer.Api.Services.ISpikeDetectionRuleService, LogJammer.Api.Services.SpikeDetectionRuleService>();
builder.Services.AddScoped<LogJammer.Api.Services.IFingerprintConfigService, LogJammer.Api.Services.FingerprintConfigService>();
builder.Services.AddScoped<LogJammer.Core.Interfaces.IFingerprintConfigRepository, LogJammer.Infrastructure.Repositories.FingerprintConfigRepository>();

// Pipeline (repos, mapper, calculator, background services)
builder.Services.AddPipelineServices();

// OpenAPI
builder.Services.AddOpenApi();

// CORS (dev: allow Vite frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database");

var app = builder.Build();

// Auto-migrate and seed (skip in Testing environment — tests manage their own DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await TagSeeder.SeedAsync(db, logger);
    await ClassificationConfigSeeder.SeedAsync(db, logger);
    await SpikeDetectionRuleSeeder.SeedAsync(db, logger);
}

// OpenAPI + Scalar UI
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Log Jammer API");
});

app.UseCors("DevCors");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHealthChecks("/healthz");

app.MapFallbackToFile("index.html");

app.Run();

// Required for WebApplicationFactory in tests
public partial class Program { }
