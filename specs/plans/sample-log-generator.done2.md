# SampleLog Generator — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a .NET 10 console app with a Terminal.Gui TUI that generates Serilog Compact JSON log files to stress-test Log Jammer's ingestion, fingerprinting, spike detection, correlation, and classification pipelines.

**Architecture:** Standalone console app at `src/SampleLog/` (not in LogJammer.slnx). Models deserialize `log-library.json` templates. `LogGenerator` renders templates via Serilog's `CompactJsonFormatter` to rolling files. Five scenario types (`Baseline`, `Spike`, `Degradation`, `Correlated`, `Volume`) run as concurrent async tasks managed by `ScenarioRunner`. Terminal.Gui provides a split-pane TUI with scrolling log view, status bar, and interactive command menu.

**Tech Stack:** .NET 10 / C# 13, Terminal.Gui v2, Serilog + Serilog.Sinks.File + Serilog.Formatting.Compact, Microsoft.Extensions.Configuration, System.Text.Json

**Design doc:** `specs/plans/sample-log-generator.draft.md`

---

### Task 1: Project Scaffolding

**Files:**
- Create: `src/SampleLog/SampleLog.csproj`
- Create: `src/SampleLog/appsettings.json`
- Create: `src/SampleLog/Program.cs` (minimal placeholder)
- Modify: `.gitignore` — add `src/SampleLog/logs/`

**Step 1: Create the project file**

Create `src/SampleLog/SampleLog.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Terminal.Gui" Version="2.*" />
    <PackageReference Include="Serilog" Version="4.*" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.*" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <None Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="log-library.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Note: `src/Directory.Build.props` provides `net10.0`, `C#13`, `nullable`, `TreatWarningsAsErrors`.

**Step 2: Create appsettings.json**

Create `src/SampleLog/appsettings.json`:

```json
{
  "Output": {
    "Directory": "./logs",
    "FilePrefix": "sample",
    "RollingSizeMB": 10,
    "MaxFiles": 5
  },
  "Defaults": {
    "BaselineEnabled": true,
    "BaselineRatePerSecond": 2,
    "SpikeCount": 50,
    "SpikeDurationSeconds": 10,
    "DegradationDurationSeconds": 120
  }
}
```

**Step 3: Create minimal Program.cs**

```csharp
Console.WriteLine("SampleLog Generator — scaffolding OK");
```

**Step 4: Add to .gitignore**

Append to `.gitignore`:

```
# SampleLog generated output
src/SampleLog/logs/
```

**Step 5: Restore and build**

Run: `dotnet restore src/SampleLog/SampleLog.csproj && dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add src/SampleLog/SampleLog.csproj src/SampleLog/appsettings.json src/SampleLog/Program.cs .gitignore
git commit -m "feat(samplelog): scaffold SampleLog console project"
```

---

### Task 2: Models & Log Library JSON

**Files:**
- Create: `src/SampleLog/Models/LogTemplate.cs`
- Create: `src/SampleLog/Models/PrebakedEntry.cs`
- Create: `src/SampleLog/Models/LogLibrary.cs`
- Create: `src/SampleLog/Models/AppConfig.cs`
- Create: `src/SampleLog/log-library.json`

**Step 1: Create model classes**

Create `src/SampleLog/Models/LogTemplate.cs`:

```csharp
using System.Text.Json.Serialization;

namespace SampleLog.Models;

public sealed class LogTemplate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("level")]
    public required string Level { get; init; }

    [JsonPropertyName("messageTemplate")]
    public required string MessageTemplate { get; init; }

    [JsonPropertyName("sourceContext")]
    public string? SourceContext { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, List<object>>? Properties { get; init; }

    [JsonPropertyName("exception")]
    public string? Exception { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }
}
```

Create `src/SampleLog/Models/PrebakedEntry.cs`:

```csharp
using System.Text.Json.Serialization;

namespace SampleLog.Models;

public sealed class PrebakedEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("level")]
    public required string Level { get; init; }

    [JsonPropertyName("raw")]
    public required string Raw { get; init; }
}
```

Create `src/SampleLog/Models/LogLibrary.cs`:

```csharp
using System.Text.Json.Serialization;

namespace SampleLog.Models;

public sealed class LogLibrary
{
    [JsonPropertyName("templates")]
    public required List<LogTemplate> Templates { get; init; }

    [JsonPropertyName("prebaked")]
    public required List<PrebakedEntry> Prebaked { get; init; }

    [JsonPropertyName("correlationGroups")]
    public required List<CorrelationGroup> CorrelationGroups { get; init; }
}

public sealed class CorrelationGroup
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("templateIds")]
    public required List<string> TemplateIds { get; init; }
}
```

Create `src/SampleLog/Models/AppConfig.cs`:

```csharp
namespace SampleLog.Models;

public sealed class OutputConfig
{
    public string Directory { get; set; } = "./logs";
    public string FilePrefix { get; set; } = "sample";
    public int RollingSizeMB { get; set; } = 10;
    public int MaxFiles { get; set; } = 5;
}

public sealed class DefaultsConfig
{
    public bool BaselineEnabled { get; set; } = true;
    public int BaselineRatePerSecond { get; set; } = 2;
    public int SpikeCount { get; set; } = 50;
    public int SpikeDurationSeconds { get; set; } = 10;
    public int DegradationDurationSeconds { get; set; } = 120;
}
```

**Step 2: Create log-library.json with ~20 templates**

Create `src/SampleLog/log-library.json` — see full content in implementation. Must include:

- **Info templates (6):** request-completed, cache-hit, health-check, user-login, job-started, job-completed
- **Warning templates (5):** slow-query, high-memory, retry-attempt, cert-expiring, pool-near-capacity
- **Error templates (8):** db-timeout, connection-refused, null-reference, auth-failure, disk-full, oom, unhandled-exception, http-503
- **Pre-baked edge cases (4):** malformed-json, long-stacktrace, unicode-message, empty-properties
- **Correlation groups (3):** database-cascade (db-timeout + connection-refused + pool-near-capacity), auth-storm (auth-failure + http-503), infrastructure-meltdown (disk-full + oom + db-timeout + http-503)

Each template has randomizable property arrays for variation.

**Step 3: Build**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/SampleLog/Models/ src/SampleLog/log-library.json
git commit -m "feat(samplelog): add models and log-library.json with 20 templates"
```

---

### Task 3: LogGenerator — Core Engine

**Files:**
- Create: `src/SampleLog/Generation/LogGenerator.cs`

**Step 1: Implement LogGenerator**

The `LogGenerator` is the core engine. It:

1. Loads `log-library.json` into `LogLibrary`
2. Configures Serilog with `CompactJsonFormatter` writing to rolling files
3. Provides `EmitRandom()` — picks a random template, resolves random property values, writes via Serilog
4. Provides `EmitTemplate(string templateId)` — emits a specific template
5. Provides `EmitPrebaked(string id)` — writes raw pre-baked entry directly to the log file
6. Tracks a running count of emitted logs and exposes it for the status bar
7. Notifies the UI of each emitted log line (via `Action<string>` callback for the TUI log view)

```csharp
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using SampleLog.Models;

namespace SampleLog.Generation;

public sealed class LogGenerator : IDisposable
{
    private readonly LogLibrary _library;
    private readonly ILogger _fileLogger;
    private readonly Random _random = new();
    private long _emittedCount;

    public long EmittedCount => Interlocked.Read(ref _emittedCount);
    public event Action<string>? OnLogEmitted; // for TUI display

    public LogLibrary Library => _library;

    public LogGenerator(LogLibrary library, OutputConfig outputConfig) { ... }
    public void EmitRandom() { ... }
    public void EmitTemplate(string templateId) { ... }
    public void EmitPrebaked(string id) { ... }
    public void Dispose() { ... }
}
```

Key implementation details:
- Serilog config: `new LoggerConfiguration().WriteTo.File(new CompactJsonFormatter(), path, rollingInterval: RollingInterval.Infinite, rollOnFileSizeLimit: true, fileSizeLimitBytes: config.RollingSizeMB * 1024 * 1024, retainedFileCountLimit: config.MaxFiles).CreateLogger()`
- Property resolution: for each key in `template.Properties`, pick a random value from the array
- Use `Log.ForContext("SourceContext", ...).ForContext(prop, value)...Write(level, exception, messageTemplate)` per event
- `OnLogEmitted` fires a short formatted string (timestamp + level + rendered message) for display in the TUI
- Pre-baked entries: replace `{{timestamp}}` placeholder with current UTC, then write raw string directly to the file via `StreamWriter` (bypassing Serilog, since these test malformed input)

**Step 2: Build**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 3: Smoke test via Program.cs**

Temporarily update `Program.cs` to load the library, create a `LogGenerator`, emit 5 random logs, and verify the output file exists and has 5 lines.

Run: `dotnet run --project src/SampleLog/SampleLog.csproj`
Expected: Console output confirms 5 logs emitted, `./logs/sample*.txt` file exists with 5 JSON lines.

Revert `Program.cs` to placeholder after verification.

**Step 4: Commit**

```bash
git add src/SampleLog/Generation/LogGenerator.cs src/SampleLog/Program.cs
git commit -m "feat(samplelog): implement LogGenerator core engine"
```

---

### Task 4: Scenarios

**Files:**
- Create: `src/SampleLog/Generation/Scenarios/IScenario.cs`
- Create: `src/SampleLog/Generation/Scenarios/BaselineScenario.cs`
- Create: `src/SampleLog/Generation/Scenarios/SpikeScenario.cs`
- Create: `src/SampleLog/Generation/Scenarios/DegradationScenario.cs`
- Create: `src/SampleLog/Generation/Scenarios/CorrelatedScenario.cs`
- Create: `src/SampleLog/Generation/Scenarios/VolumeScenario.cs`
- Create: `src/SampleLog/Generation/ScenarioRunner.cs`

**Step 1: Create IScenario interface**

```csharp
namespace SampleLog.Generation.Scenarios;

public interface IScenario
{
    string Name { get; }
    string Description { get; }
    Task RunAsync(CancellationToken ct);
}
```

**Step 2: Implement BaselineScenario**

Emits random logs at a configurable rate (default 2/sec). Uses `PeriodicTimer` for accurate pacing. Rate is mutable (changeable from TUI while running).

```csharp
public sealed class BaselineScenario(LogGenerator generator, int initialRatePerSecond) : IScenario
{
    public string Name => "Baseline";
    public string Description => $"Random logs at {RatePerSecond}/sec";
    public int RatePerSecond { get; set; } = initialRatePerSecond;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var interval = TimeSpan.FromMilliseconds(1000.0 / RatePerSecond);
            generator.EmitRandom();
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }
}
```

**Step 3: Implement SpikeScenario**

Emits a specific template `count` times over `durationSeconds`. Calculates interval = duration / count.

```csharp
public sealed class SpikeScenario(LogGenerator generator, string templateId, int count, int durationSeconds) : IScenario
{
    public string Name => $"Spike [{templateId}]";
    public string Description => $"{count} in {durationSeconds}s";

    public async Task RunAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(durationSeconds * 1000.0 / count);
        for (int i = 0; i < count && !ct.IsCancellationRequested; i++)
        {
            generator.EmitTemplate(templateId);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }
}
```

**Step 4: Implement DegradationScenario**

Starts at `startRate` logs/sec, linearly ramps to `endRate` over `durationSeconds`. Recalculates interval each step.

**Step 5: Implement CorrelatedScenario**

Takes a `CorrelationGroup` from the library. Fires all template IDs in the group in a tight burst, with small random jitter between them (50-200ms). Repeats `burstCount` times over `durationSeconds`.

**Step 6: Implement VolumeScenario**

Flood mode at a target rate. Uses tight loop with `Stopwatch` for high-throughput pacing (>100/sec). Tracks actual throughput vs target. Has presets: Light(5), Medium(50), Heavy(500), Stress(2000).

**Step 7: Implement ScenarioRunner**

Manages concurrent scenario execution:

```csharp
namespace SampleLog.Generation;

public sealed class ScenarioRunner
{
    private readonly Dictionary<string, (Task Task, CancellationTokenSource Cts)> _running = [];

    public IReadOnlyCollection<string> ActiveScenarios => _running.Keys;

    public void Start(IScenario scenario) { ... }  // Creates CTS, launches Task
    public void Stop(string name) { ... }           // Cancels CTS, removes from dict
    public void StopAll() { ... }                   // Cancels all
}
```

**Step 8: Build**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

**Step 9: Commit**

```bash
git add src/SampleLog/Generation/
git commit -m "feat(samplelog): implement 5 scenarios and ScenarioRunner"
```

---

### Task 5: Terminal.Gui TUI

**Files:**
- Create: `src/SampleLog/UI/MainWindow.cs`
- Modify: `src/SampleLog/Program.cs` — wire everything together

**Step 1: Implement MainWindow**

Uses Terminal.Gui v2 API. Three `FrameView` regions stacked vertically:

```csharp
using Terminal.Gui;
using SampleLog.Generation;
using SampleLog.Generation.Scenarios;

namespace SampleLog.UI;

public sealed class MainWindow : Toplevel
{
    private readonly LogGenerator _generator;
    private readonly ScenarioRunner _runner;
    private readonly DefaultsConfig _defaults;

    // UI elements
    private readonly TextView _logView;       // top ~70%, scrolling, read-only
    private readonly Label _statusLabel;       // 1 line, status bar
    private readonly Label _menuLabel;         // menu display
    private readonly TextField _inputField;    // command prompt

    public MainWindow(LogGenerator generator, ScenarioRunner runner, DefaultsConfig defaults)
    {
        Title = "SampleLog Generator";

        // Log output frame (top 70%)
        var logFrame = new FrameView()
        {
            Title = "Log Output",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(70)
        };
        _logView = new TextView()
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true
        };
        logFrame.Add(_logView);

        // Status bar (1 line)
        _statusLabel = new Label()
        {
            X = 0,
            Y = Pos.Bottom(logFrame),
            Width = Dim.Fill(),
            Height = 1
        };

        // Command frame (bottom)
        var cmdFrame = new FrameView()
        {
            Title = "Commands",
            X = 0,
            Y = Pos.Bottom(_statusLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _menuLabel = new Label()
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Text = "[1] Toggle Baseline  [2] Spike  [3] Degrade  [4] Correlated  [5] Rate  [6] Volume  [7] Stop All  [Q] Quit"
        };
        _inputField = new TextField()
        {
            X = 2, Y = 2,
            Width = Dim.Fill()
        };
        cmdFrame.Add(_menuLabel, _inputField);

        Add(logFrame, _statusLabel, cmdFrame);

        // Wire input handling
        _inputField.KeyDown += OnKeyDown;

        // Wire log display callback
        _generator = generator;
        _runner = runner;
        _defaults = defaults;
        _generator.OnLogEmitted += OnLogEmitted;

        // Update status bar on timer
        // Use Application.AddTimeout for periodic status updates
    }
}
```

Key behaviors:
- `OnLogEmitted`: append line to `_logView` with color attribute based on level (green/yellow/red), auto-scroll to bottom. Use `Application.Invoke()` to marshal to UI thread.
- `OnKeyDown`: handle `1`–`7` and `Q`. For commands needing parameters (spike, degrade, etc.), show a `Dialog` with `TextField` inputs.
- Status bar: updates every 500ms showing baseline state, active scenarios, total emitted count, current throughput (logs/sec calculated over last 2 seconds).

**Step 2: Wire Program.cs**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog.Formatting.Compact;
using SampleLog.Generation;
using SampleLog.Models;
using SampleLog.UI;
using Terminal.Gui;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var outputConfig = new OutputConfig();
config.GetSection("Output").Bind(outputConfig);

var defaults = new DefaultsConfig();
config.GetSection("Defaults").Bind(defaults);

var libraryJson = await File.ReadAllTextAsync("log-library.json");
var library = JsonSerializer.Deserialize<LogLibrary>(libraryJson)
    ?? throw new InvalidOperationException("Failed to load log-library.json");

using var generator = new LogGenerator(library, outputConfig);
var runner = new ScenarioRunner();

Application.Init();
var mainWindow = new MainWindow(generator, runner, defaults);
Application.Run(mainWindow);
mainWindow.Dispose();
Application.Shutdown();
```

**Step 3: Build and manual test**

Run: `dotnet build src/SampleLog/SampleLog.csproj`
Expected: Build succeeded.

Run: `dotnet run --project src/SampleLog/SampleLog.csproj`
Expected: TUI appears with split panes. Press `1` to start baseline — logs scroll in top pane. Press `Q` to quit.

**Step 4: Commit**

```bash
git add src/SampleLog/UI/ src/SampleLog/Program.cs
git commit -m "feat(samplelog): implement Terminal.Gui TUI with interactive commands"
```

---

### Task 6: Polish & Integration Test

**Files:**
- Modify: `src/SampleLog/UI/MainWindow.cs` — dialog prompts for spike/degrade/volume parameters
- Modify: `src/SampleLog/log-library.json` — tune templates if needed

**Step 1: Add parameter dialogs**

For commands that need input (Spike, Degradation, Volume), show a `Dialog` with `TextField` inputs:
- **Spike dialog:** template dropdown (from library), count (default 50), duration (default 10s)
- **Degradation dialog:** start rate (default 1), end rate (default 50), duration (default 120s)
- **Correlated dialog:** correlation group dropdown (from library)
- **Volume dialog:** preset buttons (Light 5, Medium 50, Heavy 500, Stress 2000) + custom field, duration (default 60s)

**Step 2: End-to-end test with Log Jammer**

Manual verification checklist:
1. Start SampleLog, toggle baseline — verify `./logs/sample*.txt` has Serilog Compact JSON lines
2. Trigger spike — verify burst of identical errors in the log file
3. Trigger degradation — verify increasing rate over time
4. Trigger correlated — verify related error types appearing together
5. Trigger volume at Heavy (500/sec) — verify throughput in status bar
6. Configure Log Jammer's LogFile adapter to point at `src/SampleLog/logs/` — verify errors appear in Log Jammer dashboard

**Step 3: Commit**

```bash
git add src/SampleLog/
git commit -m "feat(samplelog): add parameter dialogs and polish TUI"
```
