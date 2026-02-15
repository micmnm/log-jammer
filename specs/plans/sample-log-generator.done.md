# SampleLog Generator — Design

## Overview

A .NET 10 console app with a split-screen Terminal.Gui TUI that generates Serilog Compact JSON log files to test Log Jammer's ingestion, fingerprinting, spike detection, correlation, and classification pipelines.

## Output

- **Format:** Serilog Compact JSON (one JSON object per line) via `Serilog` + `Serilog.Sinks.File` + `Serilog.Formatting.Compact`
- **Destination:** Rolling log files in a configurable directory (default `./logs/`)
- **Rolling:** Size-based (default 10MB), max file retention (default 5 files)
- **Git:** Only the generated log output directory is added to `.gitignore`, the project itself is committed

## Log Library (`log-library.json`)

Mixed format — parameterized templates and pre-baked entries:

```json
{
  "templates": [
    {
      "id": "db-timeout",
      "level": "Error",
      "messageTemplate": "Connection to {Host}:{Port} timed out after {Timeout}ms",
      "sourceContext": "MyApp.Services.DatabaseClient",
      "properties": {
        "Host": ["db-primary", "db-replica-1", "db-replica-2"],
        "Port": [5432],
        "Timeout": [3000, 5000, 10000]
      },
      "exception": "System.TimeoutException: Connection timed out\n   at Npgsql.Internal..."
    }
  ],
  "prebaked": [
    {
      "id": "malformed-json-partial",
      "level": "Error",
      "raw": "{\"@t\":\"{{timestamp}}\",\"@mt\":\"Unexpected token"
    }
  ]
}
```

### Default templates (~15-20):

**Info:** Request completed, cache hit/miss, health check passed, user login, background job started/completed
**Warning:** Slow query, high memory usage, retry attempt, certificate expiring soon, connection pool near capacity
**Error:** DB timeout, connection refused, null reference, auth failure, disk full, OOM, unhandled exception, HTTP 503
**Pre-baked edge cases:** Truncated JSON, extremely long stack trace, unicode in message, empty properties, missing timestamp

## Scenarios

| Scenario | Behavior | Log Jammer Feature Tested |
|----------|----------|---------------------------|
| **Noisy Baseline** | Steady stream of mixed Info/Warn/Error at configurable rate (default 2/sec). Randomly picks from all templates. | Ingestion, fingerprinting, classification |
| **Spike Burst** | Fires a single error template N times in a short window (default 50 in 10 sec) | Spike detection thresholds |
| **Gradual Degradation** | Error rate increases linearly over a duration (default 120 sec) | Percentage-increase and stddev thresholds |
| **Correlated Failures** | Fires 2-4 related error templates together (e.g., timeout + connection refused + 503) | Correlation detection |
| **Volume/Load** | Flood mode at target rate (presets: Light 5/sec, Medium 50/sec, Heavy 500/sec, Stress 2000/sec) | Ingestion limits, backpressure |

### Composition:
- Baseline runs continuously in the background (toggleable)
- Other scenarios are triggered on demand from the interactive menu
- Multiple scenarios can run concurrently
- Each scenario is a `Task` with `CancellationToken` for clean stop

## Terminal UI (Terminal.Gui)

```
┌─────────────────────────────────────────────────────┐
│  SampleLog Generator                    [Running]   │
├─────────────────────────────────────────────────────┤
│                                                     │
│  14:32:01 INF  Request completed 200 /api/health    │
│  14:32:01 INF  Cache hit for key user:123           │
│  14:32:02 WRN  Slow query: 2340ms SELECT * FROM...  │
│  14:32:02 ERR  Connection to db-primary:5432 tim... │
│  14:32:03 INF  Request completed 200 /api/users     │
│  ...                                     (scrolls)  │
│                                                     │
├─────────────────────────────────────────────────────┤
│  Baseline: ON (2/sec)  │ Active: Spike [db-timeout] │
├─────────────────────────────────────────────────────┤
│  [1] Toggle Baseline   [4] Correlated Failures      │
│  [2] Trigger Spike     [5] Change Rate              │
│  [3] Gradual Degrade   [6] Volume/Load Test         │
│  [7] Stop All          [Q] Quit                     │
├─────────────────────────────────────────────────────┤
│  > _                                                │
└─────────────────────────────────────────────────────┘
```

### Three regions:
1. **Log output** (top, ~70%) — scrolling, color-coded by level (green=Info, yellow=Warn, red=Error)
2. **Status bar** (1 line) — baseline state, active scenarios, actual throughput counter
3. **Command area** (bottom) — numbered menu + prompt for scenario parameters

### Interactions:
- `1` — Toggle baseline on/off
- `2` — Spike: prompts for template, count (default 50), duration (default 10s)
- `3` — Degradation: prompts for starting rate, end rate, duration
- `4` — Correlated: select from pre-defined correlation groups
- `5` — Change baseline rate (logs/sec)
- `6` — Volume mode: select preset or custom rate + duration; shows actual vs target throughput
- `7` — Cancel all running scenarios
- `Q` — Quit

## Project Structure

```
src/SampleLog/
├── SampleLog.csproj
├── Program.cs                  # Entry point, Terminal.Gui bootstrap
├── UI/
│   └── MainWindow.cs           # Layout, regions, interaction handling
├── Generation/
│   ├── LogGenerator.cs         # Picks templates, renders properties, writes via Serilog
│   ├── ScenarioRunner.cs       # Manages scenario lifecycle (start/stop/concurrent)
│   └── Scenarios/
│       ├── BaselineScenario.cs
│       ├── SpikeScenario.cs
│       ├── DegradationScenario.cs
│       ├── CorrelatedScenario.cs
│       └── VolumeScenario.cs
├── Models/
│   ├── LogTemplate.cs
│   ├── PrebakedEntry.cs
│   └── LogLibrary.cs
├── log-library.json
└── appsettings.json
```

## Dependencies

- `Terminal.Gui` — TUI framework
- `Serilog` + `Serilog.Sinks.File` + `Serilog.Formatting.Compact` — log output
- `Microsoft.Extensions.Configuration` + `.Json` — appsettings loading
- `System.Text.Json` — built-in, log-library.json deserialization

## Configuration (`appsettings.json`)

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

## .gitignore Addition

```
# SampleLog generated output
src/SampleLog/logs/
```
