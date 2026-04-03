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
            .AllowAnyMethod());
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

app.UseMiddleware<AuthMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/healthz", () => "ok");
app.MapFallbackToFile("index.html");

app.Run();
