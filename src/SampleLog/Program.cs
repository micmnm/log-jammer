using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SampleLog.Generation;
using SampleLog.Models;
using SampleLog.MockElasticsearch;
using SampleLog.UI;
using Terminal.Gui;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var outputConfig = new OutputConfig();
config.GetSection("Output").Bind(outputConfig);

var defaults = new DefaultsConfig();
config.GetSection("Defaults").Bind(defaults);

var apiConfig = new LogJammerApiConfig();
config.GetSection("LogJammerApi").Bind(apiConfig);

var libraryJson = await File.ReadAllTextAsync("log-library.json");
var library = JsonSerializer.Deserialize<LogLibrary>(libraryJson)
    ?? throw new InvalidOperationException("Failed to load log-library.json");

using var generator = new LogGenerator(library, outputConfig);
var runner = new ScenarioRunner();

await using var mockEs = new MockElasticsearchServer(generator);
await mockEs.StartAsync();

Application.Init();
var mainWindow = new MainWindow(generator, runner, defaults, apiConfig);
Application.Run(mainWindow);
mainWindow.Dispose();
Application.Shutdown();
