using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Data.Seeding;
using LogJammer.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("LogJammer", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/logjammer-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/logjammer-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}"));

try
{
    // Database
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddLogJammerDatabase(connectionString);
    builder.Services.AddDataSourceAdapters();
    builder.Services.AddSingleton<LogJammer.Core.Interfaces.ILogFileDetectService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var allowedDirs = new List<string>
        {
            Path.Combine(env.ContentRootPath, "logs"),
            Path.Combine(env.ContentRootPath, "data"),
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "logs"))
        };
        return new LogJammer.Infrastructure.Adapters.LogFile.LogFileDetectService(allowedDirs);
    });

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

    // ProblemDetails (RFC 7807) for structured error responses
    builder.Services.AddProblemDetails();

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

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseSerilogRequestLogging();

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory in tests
public partial class Program { }
