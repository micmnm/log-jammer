# Log Jammer v2 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild Log Jammer as a lean log monitoring tool using Drain algorithm for pattern extraction, replacing the v1 ONNX/fingerprint approach.

**Architecture:** Two .NET projects (LogJammer.Engine + LogJammer.Api) with PostgreSQL. Chrome extension for Kibana bridge. React 19 frontend with 3 pages. Simple password + API key auth.

**Tech Stack:** .NET 10 / C# 13, EF Core 10, PostgreSQL 17, React 19, Vite 7, MUI 7, TanStack Query 5, Chart.js 4

**Spec:** `docs/superpowers/specs/2026-03-16-log-jammer-v2-design.md`

---

## Chunk 1: Project Scaffolding & Data Model

### Task 1: Clean up v1 projects and create v2 solution structure

**Files:**
- Delete: `src/LogJammer.Core/`, `src/LogJammer.Infrastructure/`, `src/LogJammer.Tests/`, `src/SampleLog/`
- Keep & gut: `src/LogJammer.Api/`
- Create: `src/LogJammer.Engine/LogJammer.Engine.csproj`
- Create: `src/LogJammer.Tests/LogJammer.Tests.csproj`
- Modify: `src/LogJammer.slnx`
- Modify: `src/Directory.Build.props`

- [ ] **Step 1: Delete v1 projects**

```bash
rm -rf src/LogJammer.Core src/LogJammer.Infrastructure src/LogJammer.Tests src/SampleLog
```

- [ ] **Step 2: Gut the Api project**

Remove all v1 controllers, services, DTOs, migrations from `src/LogJammer.Api/`. Keep `Program.cs` (will be rewritten), `.csproj` (will be edited), `Dockerfile` (will be updated), `appsettings.json` (will be rewritten). Delete everything else inside the project folder except those files.

- [ ] **Step 3: Create LogJammer.Engine project**

```bash
cd src && dotnet new classlib -n LogJammer.Engine --framework net10.0
```

Edit `src/LogJammer.Engine/LogJammer.Engine.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
  </ItemGroup>
</Project>
```

Remove `Class1.cs`.

- [ ] **Step 4: Create LogJammer.Tests project**

```bash
cd src && dotnet new xunit -n LogJammer.Tests --framework net10.0
```

Edit `src/LogJammer.Tests/LogJammer.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.10.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\LogJammer.Engine\LogJammer.Engine.csproj" />
    <ProjectReference Include="..\LogJammer.Api\LogJammer.Api.csproj" />
  </ItemGroup>
</Project>
```

Remove `UnitTest1.cs`.

- [ ] **Step 5: Update solution file**

Write `src/LogJammer.slnx`:
```xml
<Solution>
  <Project Path="LogJammer.Api/LogJammer.Api.csproj" />
  <Project Path="LogJammer.Engine/LogJammer.Engine.csproj" />
  <Project Path="LogJammer.Tests/LogJammer.Tests.csproj" />
</Solution>
```

- [ ] **Step 6: Update Api project references**

Edit `src/LogJammer.Api/LogJammer.Api.csproj` — remove all v1 package references and project references. Add:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
    <PackageReference Include="Scalar.AspNetCore" Version="2.6.17" />
    <PackageReference Include="Elastic.Clients.Elasticsearch" Version="9.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\LogJammer.Engine\LogJammer.Engine.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: Verify solution builds**

```bash
dotnet build src/LogJammer.slnx
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "chore: scaffold v2 project structure (Engine + Api + Tests)"
```

---

### Task 2: Define entities and DbContext

**Files:**
- Create: `src/LogJammer.Engine/Data/LogJammerDbContext.cs`
- Create: `src/LogJammer.Engine/Data/Entities/DataSource.cs`
- Create: `src/LogJammer.Engine/Data/Entities/DrainState.cs`
- Create: `src/LogJammer.Engine/Data/Entities/LogPattern.cs`
- Create: `src/LogJammer.Engine/Data/Entities/PatternOccurrence.cs`
- Create: `src/LogJammer.Engine/Data/Entities/PatternBaseline.cs`
- Create: `src/LogJammer.Engine/Data/Entities/Enums.cs`

- [ ] **Step 1: Create enums**

`src/LogJammer.Engine/Data/Entities/Enums.cs`:
```csharp
namespace LogJammer.Engine.Data.Entities;

public enum DataSourceType
{
    KibanaProxy,
    Elasticsearch
}

public enum Severity
{
    Info,
    Warning,
    Error,
    Critical
}
```

- [ ] **Step 2: Create DataSource entity**

`src/LogJammer.Engine/Data/Entities/DataSource.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogJammer.Engine.Data.Entities;

public class DataSource
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public DataSourceType Type { get; set; }

    [Column(TypeName = "jsonb")]
    public required string ConnectionConfig { get; set; }

    [MaxLength(500)]
    public string? MessageTemplate { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastPolledAt { get; set; }

    public DrainState? DrainState { get; set; }
    public ICollection<LogPattern> Patterns { get; set; } = [];
}
```

- [ ] **Step 3: Create DrainState entity**

`src/LogJammer.Engine/Data/Entities/DrainState.cs`:
```csharp
namespace LogJammer.Engine.Data.Entities;

public class DrainState
{
    public Guid Id { get; set; }

    public Guid DataSourceId { get; set; }
    public DataSource DataSource { get; set; } = null!;

    public required byte[] SerializedState { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 4: Create LogPattern entity**

`src/LogJammer.Engine/Data/Entities/LogPattern.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class LogPattern
{
    public Guid Id { get; set; }

    [MaxLength(2000)]
    public required string Template { get; set; }

    public int ClusterId { get; set; }

    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }

    [MaxLength(4000)]
    public required string SampleMessage { get; set; }

    public Severity Severity { get; set; }

    public Guid DataSourceId { get; set; }
    public DataSource DataSource { get; set; } = null!;

    public bool IsNew { get; set; } = true;

    public ICollection<PatternOccurrence> Occurrences { get; set; } = [];
    public ICollection<PatternBaseline> Baselines { get; set; } = [];
}
```

- [ ] **Step 5: Create PatternOccurrence entity**

`src/LogJammer.Engine/Data/Entities/PatternOccurrence.cs`:
```csharp
namespace LogJammer.Engine.Data.Entities;

public class PatternOccurrence
{
    public Guid Id { get; set; }

    public Guid PatternId { get; set; }
    public LogPattern Pattern { get; set; } = null!;

    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }

    public long Count { get; set; }
}
```

- [ ] **Step 6: Create PatternBaseline entity**

`src/LogJammer.Engine/Data/Entities/PatternBaseline.cs`:
```csharp
namespace LogJammer.Engine.Data.Entities;

public class PatternBaseline
{
    public Guid Id { get; set; }

    public Guid PatternId { get; set; }
    public LogPattern Pattern { get; set; } = null!;

    public int HourOfWeek { get; set; }

    public double AvgCount { get; set; }
    public double StdDevCount { get; set; }
}
```

- [ ] **Step 7: Create DbContext**

`src/LogJammer.Engine/Data/LogJammerDbContext.cs`:
```csharp
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Engine.Data;

public class LogJammerDbContext(DbContextOptions<LogJammerDbContext> options) : DbContext(options)
{
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DrainState> DrainStates => Set<DrainState>();
    public DbSet<LogPattern> LogPatterns => Set<LogPattern>();
    public DbSet<PatternOccurrence> PatternOccurrences => Set<PatternOccurrence>();
    public DbSet<PatternBaseline> PatternBaselines => Set<PatternBaseline>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataSource>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>();
        });

        modelBuilder.Entity<DrainState>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DataSourceId).IsUnique();
            e.HasOne(x => x.DataSource)
                .WithOne(x => x.DrainState)
                .HasForeignKey<DrainState>(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LogPattern>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Severity).HasConversion<string>();
            e.HasOne(x => x.DataSource)
                .WithMany(x => x.Patterns)
                .HasForeignKey(x => x.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatternOccurrence>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PatternId, x.WindowStart }).IsUnique();
            e.HasOne(x => x.Pattern)
                .WithMany(x => x.Occurrences)
                .HasForeignKey(x => x.PatternId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatternBaseline>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PatternId, x.HourOfWeek }).IsUnique();
            e.HasOne(x => x.Pattern)
                .WithMany(x => x.Baselines)
                .HasForeignKey(x => x.PatternId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

- [ ] **Step 8: Verify build**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: add v2 entities and DbContext"
```

---

### Task 3: Create initial EF migration and test with Testcontainers

**Files:**
- Create: `src/LogJammer.Engine/Data/Migrations/` (generated)
- Create: `src/LogJammer.Tests/DatabaseFixture.cs`
- Create: `src/LogJammer.Tests/Data/DbContextTests.cs`

- [ ] **Step 1: Add EF Core Design package to Api for migrations**

The Api project needs `Microsoft.EntityFrameworkCore.Design` for `dotnet ef` commands. Add it to Api `.csproj`:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

Also add a minimal `Program.cs` to Api that configures the DbContext so EF tools can discover it:
```csharp
using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LogJammerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapGet("/healthz", () => "ok");

app.Run();
```

- [ ] **Step 2: Create initial migration**

```bash
cd src/LogJammer.Api && dotnet ef migrations add InitialCreate --project ../LogJammer.Engine --output-dir Data/Migrations
```

Verify the migration file is created in `src/LogJammer.Engine/Data/Migrations/`.

- [ ] **Step 3: Create DatabaseFixture**

`src/LogJammer.Tests/DatabaseFixture.cs`:
```csharp
using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace LogJammer.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("logjammer_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public LogJammerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LogJammerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new LogJammerDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

- [ ] **Step 4: Write DB smoke test**

`src/LogJammer.Tests/Data/DbContextTests.cs`:
```csharp
using LogJammer.Engine.Data.Entities;

namespace LogJammer.Tests.Data;

public class DbContextTests(DatabaseFixture db) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task CanCreateAndQueryDataSource()
    {
        await using var ctx = db.CreateDbContext();

        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "test-source",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = """{"url":"http://localhost:9200","indexPattern":"logs-*"}"""
        };

        ctx.DataSources.Add(ds);
        await ctx.SaveChangesAsync();

        await using var readCtx = db.CreateDbContext();
        var loaded = await readCtx.DataSources.FindAsync(ds.Id);
        Assert.NotNull(loaded);
        Assert.Equal("test-source", loaded.Name);
    }

    [Fact]
    public async Task CascadeDeleteRemovesPatterns()
    {
        await using var ctx = db.CreateDbContext();

        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "cascade-test",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}"
        };

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "test pattern *",
            ClusterId = 1,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
            SampleMessage = "test pattern 123",
            Severity = Severity.Error,
            DataSourceId = ds.Id
        };

        ctx.DataSources.Add(ds);
        ctx.LogPatterns.Add(pattern);
        await ctx.SaveChangesAsync();

        ctx.DataSources.Remove(ds);
        await ctx.SaveChangesAsync();

        await using var readCtx = db.CreateDbContext();
        Assert.Null(await readCtx.LogPatterns.FindAsync(pattern.Id));
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test src/LogJammer.Tests --filter "DbContextTests" -v normal
```

Expected: 2 tests pass.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: add initial EF migration and database tests"
```

---

## Chunk 2: Drain Algorithm (C# Port)

### Task 4: Implement DrainParser core algorithm

**Files:**
- Create: `src/LogJammer.Engine/Drain/DrainParser.cs`
- Create: `src/LogJammer.Engine/Drain/DrainConfig.cs`
- Create: `src/LogJammer.Engine/Drain/DrainResult.cs`
- Create: `src/LogJammer.Engine/Drain/LogCluster.cs`
- Create: `src/LogJammer.Engine/Drain/DrainNode.cs`
- Test: `src/LogJammer.Tests/Drain/DrainParserTests.cs`

The Drain algorithm works as follows:
1. Tokenize the log message by whitespace
2. Route through a fixed-depth parse tree: length → first token → ...
3. At the leaf node, find the best matching cluster by token similarity
4. If similarity >= threshold, merge (update template: matching tokens stay, differing tokens become `*`)
5. If no match, create a new cluster

- [ ] **Step 1: Write failing tests for DrainParser**

`src/LogJammer.Tests/Drain/DrainParserTests.cs`:
```csharp
namespace LogJammer.Tests.Drain;

using LogJammer.Engine.Drain;

public class DrainParserTests
{
    private readonly DrainParser _parser = new(new DrainConfig());

    [Fact]
    public void FirstMessage_CreatesNewCluster()
    {
        var result = _parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");

        Assert.True(result.IsNewCluster);
        Assert.NotNull(result.Template);
        Assert.True(result.ClusterId > 0);
    }

    [Fact]
    public void SimilarMessages_MatchSameCluster()
    {
        _parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        var result = _parser.ParseLogMessage("Connection to db-replica:5432 timed out after 5000ms");

        Assert.False(result.IsNewCluster);
        Assert.Contains("*", result.Template);
    }

    [Fact]
    public void DifferentMessages_CreateDifferentClusters()
    {
        var r1 = _parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        var r2 = _parser.ParseLogMessage("User authentication failed for user admin");

        Assert.NotEqual(r1.ClusterId, r2.ClusterId);
    }

    [Fact]
    public void Template_PreservesFixedTokens_WildcardsVariables()
    {
        _parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        var result = _parser.ParseLogMessage("Connection to db-replica:5432 timed out after 5000ms");

        Assert.Equal("Connection to * timed out after *", result.Template);
    }

    [Fact]
    public void IdenticalMessages_MatchSameCluster()
    {
        var r1 = _parser.ParseLogMessage("Error processing request");
        var r2 = _parser.ParseLogMessage("Error processing request");

        Assert.Equal(r1.ClusterId, r2.ClusterId);
        Assert.False(r2.IsNewCluster);
    }

    [Fact]
    public void MaxClusters_EvictsLRU()
    {
        var parser = new DrainParser(new DrainConfig { MaxClusters = 3 });

        // Create 3 clusters
        parser.ParseLogMessage("alpha message one");
        parser.ParseLogMessage("beta message two");
        parser.ParseLogMessage("gamma message three");

        // Touch alpha to make beta the LRU
        parser.ParseLogMessage("alpha message one");

        // Create a 4th — should evict beta (LRU)
        parser.ParseLogMessage("delta message four");

        // Delta should exist as new
        var result = parser.ParseLogMessage("delta message four");
        Assert.False(result.IsNewCluster);
    }

    [Fact]
    public void SerializeAndRestore_PreservesState()
    {
        _parser.ParseLogMessage("Connection to db-primary:5432 timed out after 3000ms");
        _parser.ParseLogMessage("User authentication failed for user admin");

        var state = _parser.GetState();
        var restored = new DrainParser(new DrainConfig());
        restored.RestoreState(state);

        var result = restored.ParseLogMessage("Connection to db-replica:5432 timed out after 5000ms");
        Assert.False(result.IsNewCluster);
        Assert.Contains("*", result.Template);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/LogJammer.Tests --filter "DrainParserTests" -v normal
```

Expected: Compilation errors (types don't exist yet).

- [ ] **Step 3: Implement DrainConfig, DrainResult, LogCluster, DrainNode**

`src/LogJammer.Engine/Drain/DrainConfig.cs`:
```csharp
namespace LogJammer.Engine.Drain;

public class DrainConfig
{
    public double SimilarityThreshold { get; set; } = 0.4;
    public int MaxClusters { get; set; } = 1000;
    public int TreeDepth { get; set; } = 4;
}
```

`src/LogJammer.Engine/Drain/DrainResult.cs`:
```csharp
namespace LogJammer.Engine.Drain;

public record DrainResult(int ClusterId, string Template, bool IsNewCluster);
```

`src/LogJammer.Engine/Drain/LogCluster.cs`:
```csharp
namespace LogJammer.Engine.Drain;

public class LogCluster
{
    public int Id { get; set; }
    public List<string> Tokens { get; set; } = [];
    public long MatchCount { get; set; }
    public long LastMatchOrder { get; set; }

    public string GetTemplate() => string.Join(" ", Tokens);
}
```

`src/LogJammer.Engine/Drain/DrainNode.cs`:
```csharp
namespace LogJammer.Engine.Drain;

public class DrainNode
{
    public Dictionary<string, DrainNode> Children { get; set; } = [];
    public List<LogCluster> Clusters { get; set; } = [];
}
```

- [ ] **Step 4: Implement DrainParser**

`src/LogJammer.Engine/Drain/DrainParser.cs`:
```csharp
using System.Text.Json;

namespace LogJammer.Engine.Drain;

public class DrainParser(DrainConfig config)
{
    private readonly DrainNode _root = new();
    private readonly Dictionary<int, LogCluster> _clusters = [];
    private int _nextClusterId = 1;
    private long _matchOrder;

    public DrainResult ParseLogMessage(string message)
    {
        var tokens = Tokenize(message);
        if (tokens.Length == 0)
            return new DrainResult(0, "", true);

        var leafNode = TraverseTree(tokens);
        var match = FindBestMatch(leafNode, tokens);

        if (match is not null)
        {
            match.MatchCount++;
            match.LastMatchOrder = ++_matchOrder;
            UpdateTemplate(match, tokens);
            return new DrainResult(match.Id, match.GetTemplate(), false);
        }

        if (_clusters.Count >= config.MaxClusters)
            EvictLru();

        var cluster = new LogCluster
        {
            Id = _nextClusterId++,
            Tokens = [.. tokens],
            MatchCount = 1,
            LastMatchOrder = ++_matchOrder
        };

        leafNode.Clusters.Add(cluster);
        _clusters[cluster.Id] = cluster;

        return new DrainResult(cluster.Id, cluster.GetTemplate(), true);
    }

    public byte[] GetState()
    {
        var state = new DrainSerializedState
        {
            NextClusterId = _nextClusterId,
            MatchOrder = _matchOrder,
            Clusters = _clusters.Values.Select(c => new SerializedCluster
            {
                Id = c.Id,
                Tokens = c.Tokens,
                MatchCount = c.MatchCount,
                LastMatchOrder = c.LastMatchOrder
            }).ToList()
        };
        return JsonSerializer.SerializeToUtf8Bytes(state);
    }

    public void RestoreState(byte[] data)
    {
        var state = JsonSerializer.Deserialize<DrainSerializedState>(data)
            ?? throw new InvalidOperationException("Failed to deserialize Drain state");

        _nextClusterId = state.NextClusterId;
        _matchOrder = state.MatchOrder;
        _clusters.Clear();

        foreach (var sc in state.Clusters)
        {
            var cluster = new LogCluster
            {
                Id = sc.Id,
                Tokens = sc.Tokens,
                MatchCount = sc.MatchCount,
                LastMatchOrder = sc.LastMatchOrder
            };

            var leafNode = TraverseTree([.. cluster.Tokens]);
            leafNode.Clusters.Add(cluster);
            _clusters[cluster.Id] = cluster;
        }
    }

    private static string[] Tokenize(string message) =>
        message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private DrainNode TraverseTree(string[] tokens)
    {
        var node = _root;
        var depth = Math.Min(config.TreeDepth, tokens.Length);

        // First level: length bucket
        var lengthKey = tokens.Length.ToString();
        if (!node.Children.TryGetValue(lengthKey, out var lengthNode))
        {
            lengthNode = new DrainNode();
            node.Children[lengthKey] = lengthNode;
        }
        node = lengthNode;

        // Subsequent levels: token values
        for (var i = 0; i < depth - 1 && i < tokens.Length; i++)
        {
            var token = tokens[i];
            var key = HasVariable(token) ? "*" : token;

            if (!node.Children.TryGetValue(key, out var child))
            {
                child = new DrainNode();
                node.Children[key] = child;
            }
            node = child;
        }

        return node;
    }

    private LogCluster? FindBestMatch(DrainNode leafNode, string[] tokens)
    {
        LogCluster? best = null;
        var bestSim = -1.0;

        foreach (var cluster in leafNode.Clusters)
        {
            var sim = ComputeSimilarity(cluster.Tokens, tokens);
            if (sim >= config.SimilarityThreshold && sim > bestSim)
            {
                bestSim = sim;
                best = cluster;
            }
        }

        return best;
    }

    private static double ComputeSimilarity(List<string> templateTokens, string[] messageTokens)
    {
        if (templateTokens.Count != messageTokens.Length)
            return 0;

        var matchCount = 0;
        for (var i = 0; i < templateTokens.Count; i++)
        {
            if (templateTokens[i] == "*" || templateTokens[i] == messageTokens[i])
                matchCount++;
        }

        return (double)matchCount / templateTokens.Count;
    }

    private static void UpdateTemplate(LogCluster cluster, string[] tokens)
    {
        for (var i = 0; i < cluster.Tokens.Count && i < tokens.Length; i++)
        {
            if (cluster.Tokens[i] != tokens[i] && cluster.Tokens[i] != "*")
                cluster.Tokens[i] = "*";
        }
    }

    private void EvictLru()
    {
        var lru = _clusters.Values.MinBy(c => c.LastMatchOrder);
        if (lru is null) return;

        _clusters.Remove(lru.Id);
        RemoveClusterFromTree(lru);
    }

    private void RemoveClusterFromTree(LogCluster cluster)
    {
        var tokens = cluster.Tokens.ToArray();
        var leafNode = TraverseTree(tokens);
        leafNode.Clusters.Remove(cluster);
    }

    private static bool HasVariable(string token) =>
        token.Length > 1 && (
            token.All(c => char.IsDigit(c) || c == '.' || c == '-') ||
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            Guid.TryParse(token, out _));

    private class DrainSerializedState
    {
        public int NextClusterId { get; set; }
        public long MatchOrder { get; set; }
        public List<SerializedCluster> Clusters { get; set; } = [];
    }

    private class SerializedCluster
    {
        public int Id { get; set; }
        public List<string> Tokens { get; set; } = [];
        public long MatchCount { get; set; }
        public long LastMatchOrder { get; set; }
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test src/LogJammer.Tests --filter "DrainParserTests" -v normal
```

Expected: All 7 tests pass.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: implement Drain algorithm (C# port) with serialization"
```

---

### Task 5: Implement StackTracePreprocessor

**Files:**
- Create: `src/LogJammer.Engine/Processing/StackTracePreprocessor.cs`
- Test: `src/LogJammer.Tests/Processing/StackTracePreprocessorTests.cs`

- [ ] **Step 1: Write failing tests**

`src/LogJammer.Tests/Processing/StackTracePreprocessorTests.cs`:
```csharp
namespace LogJammer.Tests.Processing;

using LogJammer.Engine.Processing;

public class StackTracePreprocessorTests
{
    [Fact]
    public void DetectsStackTraceByFieldName()
    {
        var fields = new Dictionary<string, string>
        {
            ["message"] = "Something failed",
            ["stack_trace"] = "at Foo.Bar() in /app/Foo.cs:line 42\nat Baz.Qux() in /app/Baz.cs:line 10"
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.NotEqual(fields["stack_trace"], result["stack_trace"]);
        Assert.Contains("Foo.Bar", result["stack_trace"]);
    }

    [Fact]
    public void IgnoresNonStackTraceFields()
    {
        var fields = new Dictionary<string, string>
        {
            ["message"] = "Error at processing step: invalid token in request",
            ["service"] = "auth-service"
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.Equal(fields["message"], result["message"]);
        Assert.Equal(fields["service"], result["service"]);
    }

    [Fact]
    public void ExtractsTop3Frames()
    {
        var fields = new Dictionary<string, string>
        {
            ["exception_trace"] = """
                at Foo.A() in /app/Foo.cs:line 1
                at Foo.B() in /app/Foo.cs:line 2
                at Foo.C() in /app/Foo.cs:line 3
                at Foo.D() in /app/Foo.cs:line 4
                at Foo.E() in /app/Foo.cs:line 5
                """
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.Contains("Foo.A", result["exception_trace"]);
        Assert.Contains("Foo.C", result["exception_trace"]);
        Assert.DoesNotContain("Foo.D", result["exception_trace"]);
    }

    [Fact]
    public void StripsLineNumbersAndPaths()
    {
        var fields = new Dictionary<string, string>
        {
            ["stacktrace"] = "at MyApp.Services.PaymentService.Process() in /app/src/Services/PaymentService.cs:line 42"
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.DoesNotContain("line 42", result["stacktrace"]);
        Assert.DoesNotContain("/app/src", result["stacktrace"]);
        Assert.Contains("PaymentService.Process", result["stacktrace"]);
    }

    [Fact]
    public void FormatsAsSummary()
    {
        var fields = new Dictionary<string, string>
        {
            ["stack_trace"] = """
                at MyApp.PaymentService.Process() in /app/Payment.cs:line 42
                at MyApp.DatabaseClient.Execute() in /app/Db.cs:line 10
                at Npgsql.NpgsqlConnection.Open() in /npgsql/Conn.cs:line 5
                """
        };

        var result = StackTracePreprocessor.Process(fields);

        Assert.Equal(
            "at PaymentService.Process > DatabaseClient.Execute > NpgsqlConnection.Open",
            result["stack_trace"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/LogJammer.Tests --filter "StackTracePreprocessorTests" -v normal
```

- [ ] **Step 3: Implement StackTracePreprocessor**

`src/LogJammer.Engine/Processing/StackTracePreprocessor.cs`:
```csharp
using System.Text.RegularExpressions;

namespace LogJammer.Engine.Processing;

public static partial class StackTracePreprocessor
{
    private static readonly string[] StackTraceFieldIndicators = ["stack", "trace", "exception"];

    private static readonly Regex FrameRegex = GenerateFrameRegex();

    public static Dictionary<string, string> Process(Dictionary<string, string> fields)
    {
        var result = new Dictionary<string, string>(fields);

        foreach (var (key, value) in fields)
        {
            if (IsStackTraceField(key))
                result[key] = CleanStackTrace(value);
        }

        return result;
    }

    private static bool IsStackTraceField(string fieldName) =>
        StackTraceFieldIndicators.Any(indicator =>
            fieldName.Contains(indicator, StringComparison.OrdinalIgnoreCase));

    private static string CleanStackTrace(string stackTrace)
    {
        var frames = FrameRegex.Matches(stackTrace)
            .Take(3)
            .Select(m => ExtractMethodName(m.Groups[1].Value))
            .ToList();

        if (frames.Count == 0)
            return stackTrace;

        return "at " + string.Join(" > ", frames);
    }

    private static string ExtractMethodName(string fullMethodName)
    {
        // "MyApp.Services.PaymentService.Process()" → "PaymentService.Process"
        var clean = fullMethodName.TrimEnd('(', ')');
        var parts = clean.Split('.');
        return parts.Length >= 2
            ? $"{parts[^2]}.{parts[^1]}"
            : clean;
    }

    [GeneratedRegex(@"at\s+(\S+?\(?\)?)(?:\s+in\s+\S+)?(?::line\s+\d+)?", RegexOptions.Multiline)]
    private static partial Regex GenerateFrameRegex();
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test src/LogJammer.Tests --filter "StackTracePreprocessorTests" -v normal
```

Expected: All 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: implement StackTracePreprocessor with top-3 frame extraction"
```

---

### Task 6: Implement IngestionPipeline and PatternStore

**Files:**
- Create: `src/LogJammer.Engine/Processing/IngestionPipeline.cs`
- Create: `src/LogJammer.Engine/Processing/RawLogEntry.cs`
- Create: `src/LogJammer.Engine/Processing/MessageTemplateApplier.cs`
- Create: `src/LogJammer.Engine/Processing/SeverityMapper.cs`
- Create: `src/LogJammer.Engine/PatternStore.cs`
- Test: `src/LogJammer.Tests/Processing/IngestionPipelineTests.cs`
- Test: `src/LogJammer.Tests/Processing/SeverityMapperTests.cs`
- Test: `src/LogJammer.Tests/Processing/MessageTemplateApplierTests.cs`

- [ ] **Step 1: Write SeverityMapper tests**

`src/LogJammer.Tests/Processing/SeverityMapperTests.cs`:
```csharp
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;

namespace LogJammer.Tests.Processing;

public class SeverityMapperTests
{
    [Theory]
    [InlineData("debug", Severity.Info)]
    [InlineData("TRACE", Severity.Info)]
    [InlineData("info", Severity.Info)]
    [InlineData("INFO", Severity.Info)]
    [InlineData("warn", Severity.Warning)]
    [InlineData("WARNING", Severity.Warning)]
    [InlineData("error", Severity.Error)]
    [InlineData("ERROR", Severity.Error)]
    [InlineData("fatal", Severity.Critical)]
    [InlineData("CRITICAL", Severity.Critical)]
    [InlineData(null, Severity.Info)]
    [InlineData("", Severity.Info)]
    [InlineData("unknown", Severity.Info)]
    public void MapsLogLevelToSeverity(string? level, Severity expected)
    {
        Assert.Equal(expected, SeverityMapper.Map(level));
    }
}
```

- [ ] **Step 2: Implement SeverityMapper**

`src/LogJammer.Engine/Processing/SeverityMapper.cs`:
```csharp
using LogJammer.Engine.Data.Entities;

namespace LogJammer.Engine.Processing;

public static class SeverityMapper
{
    public static Severity Map(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return Severity.Info;

        return level.Trim().ToUpperInvariant() switch
        {
            "DEBUG" or "TRACE" or "VERBOSE" => Severity.Info,
            "INFO" or "INFORMATION" => Severity.Info,
            "WARN" or "WARNING" => Severity.Warning,
            "ERROR" or "ERR" => Severity.Error,
            "FATAL" or "CRITICAL" => Severity.Critical,
            _ => Severity.Info
        };
    }
}
```

- [ ] **Step 3: Run SeverityMapper tests**

```bash
dotnet test src/LogJammer.Tests --filter "SeverityMapperTests" -v normal
```

- [ ] **Step 4: Write MessageTemplateApplier tests**

`src/LogJammer.Tests/Processing/MessageTemplateApplierTests.cs`:
```csharp
using LogJammer.Engine.Processing;

namespace LogJammer.Tests.Processing;

public class MessageTemplateApplierTests
{
    [Fact]
    public void AppliesTemplate_SubstitutesFields()
    {
        var fields = new Dictionary<string, string>
        {
            ["service.name"] = "payment-service",
            ["error.type"] = "TimeoutException",
            ["message"] = "Connection timed out"
        };

        var result = MessageTemplateApplier.Apply("{service.name} | {error.type} | {message}", fields);

        Assert.Equal("payment-service | TimeoutException | Connection timed out", result);
    }

    [Fact]
    public void MissingField_LeavesPlaceholder()
    {
        var fields = new Dictionary<string, string>
        {
            ["message"] = "error occurred"
        };

        var result = MessageTemplateApplier.Apply("{service.name} | {message}", fields);

        Assert.Equal("{service.name} | error occurred", result);
    }

    [Fact]
    public void NullTemplate_ReturnsMessage()
    {
        var entry = new RawLogEntry
        {
            Message = "raw message",
            Timestamp = DateTimeOffset.UtcNow
        };

        var result = MessageTemplateApplier.Apply(null, entry);

        Assert.Equal("raw message", result);
    }
}
```

- [ ] **Step 5: Implement RawLogEntry and MessageTemplateApplier**

`src/LogJammer.Engine/Processing/RawLogEntry.cs`:
```csharp
namespace LogJammer.Engine.Processing;

public class RawLogEntry
{
    public required string Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Level { get; set; }
    public Dictionary<string, string>? Fields { get; set; }
}
```

`src/LogJammer.Engine/Processing/MessageTemplateApplier.cs`:
```csharp
using System.Text.RegularExpressions;

namespace LogJammer.Engine.Processing;

public static partial class MessageTemplateApplier
{
    public static string Apply(string? template, Dictionary<string, string> fields)
    {
        if (template is null)
            return fields.GetValueOrDefault("message") ?? "";

        return PlaceholderRegex().Replace(template, match =>
        {
            var fieldName = match.Groups[1].Value;
            return fields.GetValueOrDefault(fieldName) ?? match.Value;
        });
    }

    public static string Apply(string? template, RawLogEntry entry)
    {
        if (template is null || entry.Fields is null)
            return entry.Message;

        return Apply(template, entry.Fields);
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex PlaceholderRegex();
}
```

- [ ] **Step 6: Run MessageTemplateApplier tests**

```bash
dotnet test src/LogJammer.Tests --filter "MessageTemplateApplierTests" -v normal
```

- [ ] **Step 7: Implement PatternStore**

`src/LogJammer.Engine/PatternStore.cs`:
```csharp
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Drain;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Engine;

public class PatternStore(LogJammerDbContext db)
{
    public async Task RecordOccurrenceAsync(
        DrainResult drainResult,
        Severity severity,
        string rawMessage,
        Guid dataSourceId,
        DateTimeOffset timestamp)
    {
        var pattern = await db.LogPatterns
            .FirstOrDefaultAsync(p => p.DataSourceId == dataSourceId && p.ClusterId == drainResult.ClusterId);

        if (pattern is null)
        {
            pattern = new LogPattern
            {
                Id = Guid.NewGuid(),
                Template = drainResult.Template,
                ClusterId = drainResult.ClusterId,
                FirstSeen = timestamp,
                LastSeen = timestamp,
                SampleMessage = rawMessage,
                Severity = severity,
                DataSourceId = dataSourceId,
                IsNew = true
            };
            db.LogPatterns.Add(pattern);
        }
        else
        {
            pattern.LastSeen = timestamp;
            pattern.SampleMessage = rawMessage;
            pattern.Template = drainResult.Template;
        }

        var windowStart = new DateTimeOffset(
            timestamp.UtcDateTime.Year, timestamp.UtcDateTime.Month, timestamp.UtcDateTime.Day,
            timestamp.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddHours(1);

        var occurrence = await db.PatternOccurrences
            .FirstOrDefaultAsync(o => o.PatternId == pattern.Id && o.WindowStart == windowStart);

        if (occurrence is null)
        {
            db.PatternOccurrences.Add(new PatternOccurrence
            {
                Id = Guid.NewGuid(),
                PatternId = pattern.Id,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                Count = 1
            });
        }
        else
        {
            occurrence.Count++;
        }

        await db.SaveChangesAsync();
    }

    public async Task<LogPattern?> GetPatternAsync(Guid patternId) =>
        await db.LogPatterns
            .AsNoTracking()
            .Include(p => p.DataSource)
            .FirstOrDefaultAsync(p => p.Id == patternId);

    public async Task AcknowledgeAsync(Guid patternId)
    {
        await db.LogPatterns
            .Where(p => p.Id == patternId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsNew, false));
    }

    public async Task<int> AcknowledgeAllAsync(Guid? dataSourceId = null)
    {
        var query = db.LogPatterns.Where(p => p.IsNew);
        if (dataSourceId.HasValue)
            query = query.Where(p => p.DataSourceId == dataSourceId.Value);

        return await query.ExecuteUpdateAsync(s => s.SetProperty(p => p.IsNew, false));
    }
}
```

- [ ] **Step 8: Implement IngestionPipeline**

`src/LogJammer.Engine/Processing/IngestionPipeline.cs`:
```csharp
using System.Collections.Concurrent;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Drain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Engine.Processing;

public class IngestionPipeline(
    IServiceScopeFactory scopeFactory,
    DrainConfig drainConfig)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<Guid, DrainParser> _parsers = new();

    public async Task ProcessEntriesAsync(
        IEnumerable<RawLogEntry> entries,
        Guid dataSourceId,
        string? messageTemplate)
    {
        var semaphore = _locks.GetOrAdd(dataSourceId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            var parser = await GetOrCreateParserAsync(dataSourceId);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
            var store = new PatternStore(db);

            foreach (var entry in entries)
            {
                var message = entry.Fields is not null
                    ? MessageTemplateApplier.Apply(messageTemplate, StackTracePreprocessor.Process(entry.Fields))
                    : entry.Message;

                var severity = SeverityMapper.Map(entry.Level);
                var result = parser.ParseLogMessage(message);
                await store.RecordOccurrenceAsync(result, severity, entry.Message, dataSourceId, entry.Timestamp);
            }

            // Persist Drain state
            var state = parser.GetState();
            var drainState = await db.DrainStates.FirstOrDefaultAsync(s => s.DataSourceId == dataSourceId);
            if (drainState is null)
            {
                db.DrainStates.Add(new Data.Entities.DrainState
                {
                    Id = Guid.NewGuid(),
                    DataSourceId = dataSourceId,
                    SerializedState = state,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                drainState.SerializedState = state;
                drainState.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<DrainParser> GetOrCreateParserAsync(Guid dataSourceId)
    {
        if (_parsers.TryGetValue(dataSourceId, out var existing))
            return existing;

        var parser = new DrainParser(drainConfig);

        // Try restore from DB
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
        var savedState = await db.DrainStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DataSourceId == dataSourceId);

        if (savedState is not null)
            parser.RestoreState(savedState.SerializedState);

        _parsers[dataSourceId] = parser;
        return parser;
    }
}
```

- [ ] **Step 9: Write integration test for IngestionPipeline**

`src/LogJammer.Tests/Processing/IngestionPipelineTests.cs`:
```csharp
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Drain;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogJammer.Tests.Processing;

public class IngestionPipelineTests(DatabaseFixture db) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task ProcessEntries_CreatesPatternAndOccurrence()
    {
        await using var ctx = db.CreateDbContext();
        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "test",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "{}"
        };
        ctx.DataSources.Add(ds);
        await ctx.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddDbContext<LogJammerDbContext>(o => o.UseNpgsql(db.ConnectionString));
        var sp = services.BuildServiceProvider();

        var pipeline = new IngestionPipeline(sp.GetRequiredService<IServiceScopeFactory>(), new DrainConfig());

        var entries = new[]
        {
            new RawLogEntry
            {
                Message = "Connection to db-primary:5432 timed out after 3000ms",
                Timestamp = DateTimeOffset.UtcNow,
                Level = "error"
            }
        };

        await pipeline.ProcessEntriesAsync(entries, ds.Id, null);

        await using var readCtx = db.CreateDbContext();
        var patterns = await readCtx.LogPatterns.Where(p => p.DataSourceId == ds.Id).ToListAsync();
        Assert.Single(patterns);
        Assert.True(patterns[0].IsNew);
        Assert.Equal(Severity.Error, patterns[0].Severity);

        var occurrences = await readCtx.PatternOccurrences.Where(o => o.PatternId == patterns[0].Id).ToListAsync();
        Assert.Single(occurrences);
        Assert.Equal(1, occurrences[0].Count);
    }
}
```

Note: `DatabaseFixture` needs a `ConnectionString` property. Update it:

Add to `DatabaseFixture.cs`:
```csharp
public string ConnectionString => _container.GetConnectionString();
```

- [ ] **Step 10: Run all tests**

```bash
dotnet test src/LogJammer.Tests -v normal
```

Expected: All tests pass.

- [ ] **Step 11: Commit**

```bash
git add -A && git commit -m "feat: implement IngestionPipeline, PatternStore, SeverityMapper, MessageTemplateApplier"
```

---

### Task 7: Implement BaselineCalculator

**Files:**
- Create: `src/LogJammer.Engine/BaselineCalculator.cs`
- Test: `src/LogJammer.Tests/BaselineCalculatorTests.cs`

- [ ] **Step 1: Write failing tests**

`src/LogJammer.Tests/BaselineCalculatorTests.cs`:
```csharp
using LogJammer.Engine;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Tests;

public class BaselineCalculatorTests(DatabaseFixture db) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task RecalculateBaselines_ComputesAvgAndStdDev()
    {
        await using var ctx = db.CreateDbContext();

        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "baseline-test",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "{}"
        };
        ctx.DataSources.Add(ds);

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "baseline test *",
            ClusterId = 100,
            FirstSeen = DateTimeOffset.UtcNow.AddDays(-14),
            LastSeen = DateTimeOffset.UtcNow,
            SampleMessage = "baseline test 1",
            Severity = Severity.Error,
            DataSourceId = ds.Id
        };
        ctx.LogPatterns.Add(pattern);

        // Add occurrences for 4 weeks, same hour-of-week, varying counts
        var now = DateTimeOffset.UtcNow;
        var hourOfWeek = (int)now.DayOfWeek * 24 + now.Hour;

        for (var week = 0; week < 4; week++)
        {
            var windowStart = new DateTimeOffset(
                now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
                now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero).AddDays(-7 * week);

            ctx.PatternOccurrences.Add(new PatternOccurrence
            {
                Id = Guid.NewGuid(),
                PatternId = pattern.Id,
                WindowStart = windowStart,
                WindowEnd = windowStart.AddHours(1),
                Count = 10 + week * 5 // 10, 15, 20, 25
            });
        }

        await ctx.SaveChangesAsync();

        var calculator = new BaselineCalculator(ctx);
        await calculator.RecalculateBaselinesAsync(pattern.Id);

        await using var readCtx = db.CreateDbContext();
        var baseline = await readCtx.PatternBaselines
            .FirstOrDefaultAsync(b => b.PatternId == pattern.Id && b.HourOfWeek == hourOfWeek);

        Assert.NotNull(baseline);
        Assert.Equal(17.5, baseline.AvgCount, 0.1); // avg of 10,15,20,25
        Assert.True(baseline.StdDevCount > 0);
    }

    [Fact]
    public async Task GetCurrentComparison_ReturnsDeviation()
    {
        await using var ctx = db.CreateDbContext();

        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = "comparison-test",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "{}"
        };
        ctx.DataSources.Add(ds);

        var now = DateTimeOffset.UtcNow;
        var hourOfWeek = (int)now.DayOfWeek * 24 + now.Hour;

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "comparison *",
            ClusterId = 200,
            FirstSeen = now,
            LastSeen = now,
            SampleMessage = "comparison test",
            Severity = Severity.Warning,
            DataSourceId = ds.Id
        };
        ctx.LogPatterns.Add(pattern);

        ctx.PatternBaselines.Add(new PatternBaseline
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            HourOfWeek = hourOfWeek,
            AvgCount = 5.0,
            StdDevCount = 2.0
        });

        var windowStart = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);

        ctx.PatternOccurrences.Add(new PatternOccurrence
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            WindowStart = windowStart,
            WindowEnd = windowStart.AddHours(1),
            Count = 47
        });

        await ctx.SaveChangesAsync();

        var calculator = new BaselineCalculator(ctx);
        var comparison = await calculator.GetCurrentComparisonAsync(pattern.Id);

        Assert.NotNull(comparison);
        Assert.Equal(47, comparison.CurrentRate);
        Assert.Equal(5.0, comparison.ExpectedRate);
        Assert.True(comparison.StdDevsFromMean > 20); // (47-5)/2 = 21
    }
}
```

- [ ] **Step 2: Implement BaselineCalculator**

`src/LogJammer.Engine/BaselineCalculator.cs`:
```csharp
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Engine;

public record BaselineComparison(long CurrentRate, double ExpectedRate, double StdDevsFromMean);

public class BaselineCalculator(LogJammerDbContext db)
{
    public async Task RecalculateBaselinesAsync(Guid? patternId = null)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-28); // 4 weeks

        var query = db.PatternOccurrences
            .Where(o => o.WindowStart >= cutoff);

        if (patternId.HasValue)
            query = query.Where(o => o.PatternId == patternId.Value);

        var grouped = await query
            .GroupBy(o => new
            {
                o.PatternId,
                HourOfWeek = ((int)o.WindowStart.DayOfWeek) * 24 + o.WindowStart.Hour
            })
            .Select(g => new
            {
                g.Key.PatternId,
                g.Key.HourOfWeek,
                Counts = g.Select(o => (double)o.Count).ToList()
            })
            .ToListAsync();

        foreach (var group in grouped)
        {
            var avg = group.Counts.Average();
            var stdDev = group.Counts.Count > 1
                ? Math.Sqrt(group.Counts.Sum(c => Math.Pow(c - avg, 2)) / (group.Counts.Count - 1))
                : 0;

            var existing = await db.PatternBaselines
                .FirstOrDefaultAsync(b => b.PatternId == group.PatternId && b.HourOfWeek == group.HourOfWeek);

            if (existing is null)
            {
                db.PatternBaselines.Add(new PatternBaseline
                {
                    Id = Guid.NewGuid(),
                    PatternId = group.PatternId,
                    HourOfWeek = group.HourOfWeek,
                    AvgCount = avg,
                    StdDevCount = stdDev
                });
            }
            else
            {
                existing.AvgCount = avg;
                existing.StdDevCount = stdDev;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<BaselineComparison?> GetCurrentComparisonAsync(Guid patternId)
    {
        var now = DateTimeOffset.UtcNow;
        var hourOfWeek = (int)now.DayOfWeek * 24 + now.Hour;

        var baseline = await db.PatternBaselines
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.PatternId == patternId && b.HourOfWeek == hourOfWeek);

        var windowStart = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);

        var currentCount = await db.PatternOccurrences
            .Where(o => o.PatternId == patternId && o.WindowStart == windowStart)
            .Select(o => o.Count)
            .FirstOrDefaultAsync();

        if (baseline is null)
            return new BaselineComparison(currentCount, 0, 0);

        var stdDevs = baseline.StdDevCount > 0
            ? (currentCount - baseline.AvgCount) / baseline.StdDevCount
            : 0;

        return new BaselineComparison(currentCount, baseline.AvgCount, stdDevs);
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test src/LogJammer.Tests --filter "BaselineCalculatorTests" -v normal
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: implement BaselineCalculator with hour-of-week statistical baselines"
```

---

## Chunk 3: API Layer & Authentication

### Task 8: Authentication middleware and login endpoint

**Files:**
- Create: `src/LogJammer.Api/Auth/AuthMiddleware.cs`
- Create: `src/LogJammer.Api/Auth/AuthSettings.cs`
- Create: `src/LogJammer.Api/Auth/TokenService.cs`
- Create: `src/LogJammer.Api/Controllers/AuthController.cs`
- Create: `src/LogJammer.Api/Dtos/AuthDtos.cs`
- Modify: `src/LogJammer.Api/appsettings.json`
- Modify: `src/LogJammer.Api/Program.cs`
- Test: `src/LogJammer.Tests/Api/AuthTests.cs`

- [ ] **Step 1: Create AuthSettings and appsettings.json**

`src/LogJammer.Api/Auth/AuthSettings.cs`:
```csharp
namespace LogJammer.Api.Auth;

public class AuthSettings
{
    public required string Password { get; set; }
    public required string ApiKey { get; set; }
}
```

`src/LogJammer.Api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=logjammer;Username=logjammer;Password=logjammer"
  },
  "Auth": {
    "Password": "changeme",
    "ApiKey": "changeme"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- [ ] **Step 2: Create TokenService**

`src/LogJammer.Api/Auth/TokenService.cs`:
```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LogJammer.Api.Auth;

public class TokenService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromHours(24);

    public string CreateToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _tokens[token] = DateTimeOffset.UtcNow.Add(TokenExpiry);
        CleanExpired();
        return token;
    }

    public bool ValidateToken(string token) =>
        _tokens.TryGetValue(token, out var expiry) && expiry > DateTimeOffset.UtcNow;

    private void CleanExpired()
    {
        var expired = _tokens.Where(kv => kv.Value <= DateTimeOffset.UtcNow).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _tokens.TryRemove(key, out _);
    }
}
```

- [ ] **Step 3: Create AuthMiddleware**

`src/LogJammer.Api/Auth/AuthMiddleware.cs`:
```csharp
using Microsoft.Extensions.Options;

namespace LogJammer.Api.Auth;

public class AuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<AuthSettings> settings, TokenService tokenService)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for login and health
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Only protect /api routes
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Check Bearer token
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..];
            if (tokenService.ValidateToken(token))
            {
                await next(context);
                return;
            }
        }

        // Check API key
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (apiKey is not null && apiKey == settings.Value.ApiKey)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
    }
}
```

- [ ] **Step 4: Create AuthController and DTOs**

`src/LogJammer.Api/Dtos/AuthDtos.cs`:
```csharp
namespace LogJammer.Api.Dtos;

public record LoginRequest(string Password);
public record LoginResponse(string Token);
```

`src/LogJammer.Api/Controllers/AuthController.cs`:
```csharp
using LogJammer.Api.Auth;
using LogJammer.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IOptions<AuthSettings> settings, TokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (request.Password != settings.Value.Password)
            return Unauthorized("Invalid password");

        var token = tokenService.CreateToken();
        return Ok(new LoginResponse(token));
    }
}
```

- [ ] **Step 5: Wire up in Program.cs**

`src/LogJammer.Api/Program.cs`:
```csharp
using LogJammer.Api.Auth;
using LogJammer.Engine.Data;
using LogJammer.Engine.Drain;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LogJammerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton(new DrainConfig());
builder.Services.AddSingleton<IngestionPipeline>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors("DevCors");
}

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<AuthMiddleware>();
app.MapControllers();
app.MapGet("/healthz", () => "ok");

app.Run();
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 7: Write auth tests**

`src/LogJammer.Tests/Api/AuthTests.cs`:
```csharp
using LogJammer.Api.Auth;
using Microsoft.Extensions.Options;

namespace LogJammer.Tests.Api;

public class AuthTests
{
    [Fact]
    public void TokenService_CreateAndValidate()
    {
        var service = new TokenService();
        var token = service.CreateToken();

        Assert.True(service.ValidateToken(token));
        Assert.False(service.ValidateToken("invalid-token"));
    }

    [Fact]
    public void Login_CorrectPassword_ReturnsToken()
    {
        var settings = Options.Create(new AuthSettings { Password = "test123", ApiKey = "key123" });
        var tokenService = new TokenService();
        var controller = new LogJammer.Api.Controllers.AuthController(settings, tokenService);

        var result = controller.Login(new LogJammer.Api.Dtos.LoginRequest("test123"));

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
    }

    [Fact]
    public void Login_WrongPassword_Returns401()
    {
        var settings = Options.Create(new AuthSettings { Password = "test123", ApiKey = "key123" });
        var tokenService = new TokenService();
        var controller = new LogJammer.Api.Controllers.AuthController(settings, tokenService);

        var result = controller.Login(new LogJammer.Api.Dtos.LoginRequest("wrong"));

        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(result);
    }
}
```

- [ ] **Step 8: Run tests**

```bash
dotnet test src/LogJammer.Tests --filter "AuthTests" -v normal
```

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: add password + API key authentication"
```

---

### Task 9: DataSources and Ingest controllers

**Files:**
- Create: `src/LogJammer.Api/Controllers/DataSourcesController.cs`
- Create: `src/LogJammer.Api/Controllers/IngestController.cs`
- Create: `src/LogJammer.Api/Dtos/DataSourceDtos.cs`
- Create: `src/LogJammer.Api/Dtos/IngestDtos.cs`

- [ ] **Step 1: Create DTOs**

`src/LogJammer.Api/Dtos/DataSourceDtos.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record CreateDataSourceRequest(
    [MaxLength(200)] string Name,
    DataSourceType Type,
    string ConnectionConfig,
    string? MessageTemplate);

public record UpdateDataSourceRequest(
    [MaxLength(200)] string? Name,
    string? ConnectionConfig,
    string? MessageTemplate,
    bool? Enabled);

public record DataSourceResponse(
    Guid Id,
    string Name,
    DataSourceType Type,
    string ConnectionConfig,
    string? MessageTemplate,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastPolledAt);

public record FieldInfo(string Name, string? SampleValue);
```

`src/LogJammer.Api/Dtos/IngestDtos.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record IngestRequest([MaxLength(10000)] IngestEntry[] Entries);

public record IngestEntry(string Message, DateTimeOffset Timestamp, string? Level);

public record IngestResponse(int Accepted);
```

- [ ] **Step 2: Create DataSourcesController**

`src/LogJammer.Api/Controllers/DataSourcesController.cs`:
```csharp
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/datasources")]
public class DataSourcesController(LogJammerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.DataSources.AsNoTracking()
            .Select(d => new DataSourceResponse(
                d.Id, d.Name, d.Type, d.ConnectionConfig,
                d.MessageTemplate, d.Enabled, d.CreatedAt, d.LastPolledAt))
            .ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ds = await db.DataSources.FindAsync(id);
        if (ds is null) return NotFound();
        return Ok(new DataSourceResponse(
            ds.Id, ds.Name, ds.Type, ds.ConnectionConfig,
            ds.MessageTemplate, ds.Enabled, ds.CreatedAt, ds.LastPolledAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDataSourceRequest request)
    {
        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            ConnectionConfig = request.ConnectionConfig,
            MessageTemplate = request.MessageTemplate
        };
        db.DataSources.Add(ds);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ds.Id },
            new DataSourceResponse(ds.Id, ds.Name, ds.Type, ds.ConnectionConfig,
                ds.MessageTemplate, ds.Enabled, ds.CreatedAt, ds.LastPolledAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDataSourceRequest request)
    {
        var ds = await db.DataSources.FindAsync(id);
        if (ds is null) return NotFound();

        if (request.Name is not null) ds.Name = request.Name;
        if (request.ConnectionConfig is not null) ds.ConnectionConfig = request.ConnectionConfig;
        if (request.MessageTemplate is not null) ds.MessageTemplate = request.MessageTemplate;
        if (request.Enabled.HasValue) ds.Enabled = request.Enabled.Value;

        await db.SaveChangesAsync();
        return Ok(new DataSourceResponse(
            ds.Id, ds.Name, ds.Type, ds.ConnectionConfig,
            ds.MessageTemplate, ds.Enabled, ds.CreatedAt, ds.LastPolledAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ds = await db.DataSources.FindAsync(id);
        if (ds is null) return NotFound();
        db.DataSources.Remove(ds);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
```

- [ ] **Step 3: Create IngestController**

`src/LogJammer.Api/Controllers/IngestController.cs`:
```csharp
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.AspNetCore.Mvc;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController(LogJammerDbContext db, IngestionPipeline pipeline) : ControllerBase
{
    [HttpPost("{dataSourceId:guid}")]
    public async Task<IActionResult> Ingest(Guid dataSourceId, [FromBody] IngestRequest request)
    {
        var ds = await db.DataSources.FindAsync(dataSourceId);
        if (ds is null) return NotFound("Data source not found");

        var entries = request.Entries.Select(e => new RawLogEntry
        {
            Message = e.Message,
            Timestamp = e.Timestamp,
            Level = e.Level
        });

        await pipeline.ProcessEntriesAsync(entries, dataSourceId, ds.MessageTemplate);

        return Ok(new IngestResponse(request.Entries.Length));
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add DataSources and Ingest API controllers"
```

---

### Task 10: Patterns and Dashboard controllers

**Files:**
- Create: `src/LogJammer.Api/Controllers/PatternsController.cs`
- Create: `src/LogJammer.Api/Controllers/DashboardController.cs`
- Create: `src/LogJammer.Api/Dtos/PatternDtos.cs`
- Create: `src/LogJammer.Api/Dtos/DashboardDtos.cs`

- [ ] **Step 1: Create DTOs**

`src/LogJammer.Api/Dtos/PatternDtos.cs`:
```csharp
using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record PatternListItem(
    Guid Id,
    string Template,
    Severity Severity,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    bool IsNew,
    long CurrentRate,
    double ExpectedRate,
    double StdDevsFromMean,
    string DataSourceName);

public record PatternDetailResponse(
    Guid Id,
    string Template,
    string SampleMessage,
    Severity Severity,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    bool IsNew,
    string DataSourceName,
    long CurrentRate,
    double ExpectedRate,
    double StdDevsFromMean,
    List<OccurrencePoint> Occurrences,
    List<BaselineBand> BaselineBands);

public record OccurrencePoint(DateTimeOffset WindowStart, long Count);
public record BaselineBand(int HourOfWeek, double AvgCount, double StdDevCount);
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

`src/LogJammer.Api/Dtos/DashboardDtos.cs`:
```csharp
using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record DashboardResponse(
    int TotalPatterns,
    int NewPatternCount,
    long IngestionRatePerHour,
    List<AnomalyItem> TopAnomalies,
    List<NewPatternItem> NewPatterns);

public record AnomalyItem(
    Guid PatternId,
    string Template,
    Severity Severity,
    long CurrentRate,
    double ExpectedRate,
    double StdDevsFromMean,
    string DataSourceName);

public record NewPatternItem(
    Guid PatternId,
    string Template,
    Severity Severity,
    DateTimeOffset FirstSeen,
    string DataSourceName);
```

- [ ] **Step 2: Create PatternsController**

`src/LogJammer.Api/Controllers/PatternsController.cs`:
```csharp
using LogJammer.Api.Dtos;
using LogJammer.Engine;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/patterns")]
public class PatternsController(LogJammerDbContext db, BaselineCalculator baselineCalc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? dataSourceId,
        [FromQuery] Severity? severity,
        [FromQuery] bool? isNew,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = db.LogPatterns.AsNoTracking().Include(p => p.DataSource).AsQueryable();

        if (dataSourceId.HasValue) query = query.Where(p => p.DataSourceId == dataSourceId.Value);
        if (severity.HasValue) query = query.Where(p => p.Severity == severity.Value);
        if (isNew.HasValue) query = query.Where(p => p.IsNew == isNew.Value);
        if (from.HasValue) query = query.Where(p => p.LastSeen >= from.Value);
        if (to.HasValue) query = query.Where(p => p.LastSeen <= to.Value);

        var totalCount = await query.CountAsync();
        var patterns = await query
            .OrderByDescending(p => p.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<PatternListItem>();
        foreach (var p in patterns)
        {
            var comparison = await baselineCalc.GetCurrentComparisonAsync(p.Id);
            items.Add(new PatternListItem(
                p.Id, p.Template, p.Severity, p.FirstSeen, p.LastSeen, p.IsNew,
                comparison?.CurrentRate ?? 0,
                comparison?.ExpectedRate ?? 0,
                comparison?.StdDevsFromMean ?? 0,
                p.DataSource.Name));
        }

        return Ok(new PagedResult<PatternListItem>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var pattern = await db.LogPatterns.AsNoTracking()
            .Include(p => p.DataSource)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pattern is null) return NotFound();

        var comparison = await baselineCalc.GetCurrentComparisonAsync(id);

        var occurrences = await db.PatternOccurrences.AsNoTracking()
            .Where(o => o.PatternId == id)
            .OrderByDescending(o => o.WindowStart)
            .Take(168) // 7 days of hourly data
            .OrderBy(o => o.WindowStart)
            .Select(o => new OccurrencePoint(o.WindowStart, o.Count))
            .ToListAsync();

        var baselines = await db.PatternBaselines.AsNoTracking()
            .Where(b => b.PatternId == id)
            .Select(b => new BaselineBand(b.HourOfWeek, b.AvgCount, b.StdDevCount))
            .ToListAsync();

        return Ok(new PatternDetailResponse(
            pattern.Id, pattern.Template, pattern.SampleMessage,
            pattern.Severity, pattern.FirstSeen, pattern.LastSeen, pattern.IsNew,
            pattern.DataSource.Name,
            comparison?.CurrentRate ?? 0,
            comparison?.ExpectedRate ?? 0,
            comparison?.StdDevsFromMean ?? 0,
            occurrences, baselines));
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var store = new PatternStore(db);
        await store.AcknowledgeAsync(id);
        return NoContent();
    }

    [HttpPost("acknowledge-all")]
    public async Task<IActionResult> AcknowledgeAll([FromQuery] Guid? dataSourceId)
    {
        var store = new PatternStore(db);
        var count = await store.AcknowledgeAllAsync(dataSourceId);
        return Ok(new { acknowledged = count });
    }
}
```

- [ ] **Step 3: Create DashboardController**

`src/LogJammer.Api/Controllers/DashboardController.cs`:
```csharp
using LogJammer.Api.Dtos;
using LogJammer.Engine;
using LogJammer.Engine.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(LogJammerDbContext db, BaselineCalculator baselineCalc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var totalPatterns = await db.LogPatterns.CountAsync();
        var newPatternCount = await db.LogPatterns.CountAsync(p => p.IsNew);

        var windowStart = new DateTimeOffset(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day,
            DateTime.UtcNow.Hour, 0, 0, TimeSpan.Zero);
        var ingestionRate = await db.PatternOccurrences
            .Where(o => o.WindowStart == windowStart)
            .SumAsync(o => o.Count);

        // Top anomalies
        var patterns = await db.LogPatterns.AsNoTracking()
            .Include(p => p.DataSource)
            .ToListAsync();

        var anomalies = new List<AnomalyItem>();
        foreach (var p in patterns)
        {
            var comparison = await baselineCalc.GetCurrentComparisonAsync(p.Id);
            if (comparison is not null && Math.Abs(comparison.StdDevsFromMean) > 1)
            {
                anomalies.Add(new AnomalyItem(
                    p.Id, p.Template, p.Severity,
                    comparison.CurrentRate, comparison.ExpectedRate, comparison.StdDevsFromMean,
                    p.DataSource.Name));
            }
        }

        var topAnomalies = anomalies
            .OrderByDescending(a => Math.Abs(a.StdDevsFromMean))
            .Take(10)
            .ToList();

        var newPatterns = await db.LogPatterns.AsNoTracking()
            .Include(p => p.DataSource)
            .Where(p => p.IsNew)
            .OrderByDescending(p => p.FirstSeen)
            .Take(50)
            .Select(p => new NewPatternItem(
                p.Id, p.Template, p.Severity, p.FirstSeen, p.DataSource.Name))
            .ToListAsync();

        return Ok(new DashboardResponse(
            totalPatterns, newPatternCount, ingestionRate, topAnomalies, newPatterns));
    }
}
```

- [ ] **Step 4: Register BaselineCalculator in Program.cs**

Add to `Program.cs` services:
```csharp
builder.Services.AddScoped<BaselineCalculator>();
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: add Patterns and Dashboard API controllers"
```

---

### Task 11: Background services

**Files:**
- Create: `src/LogJammer.Api/BackgroundServices/BaselineRecalculationService.cs`
- Create: `src/LogJammer.Api/BackgroundServices/DataRetentionService.cs`

- [ ] **Step 1: Implement BaselineRecalculationService**

`src/LogJammer.Api/BackgroundServices/BaselineRecalculationService.cs`:
```csharp
using LogJammer.Engine;
using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.BackgroundServices;

public class BaselineRecalculationService(
    IServiceScopeFactory scopeFactory,
    ILogger<BaselineRecalculationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
                var calculator = new BaselineCalculator(db);
                await calculator.RecalculateBaselinesAsync();
                logger.LogInformation("Baseline recalculation completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Baseline recalculation failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

- [ ] **Step 2: Implement DataRetentionService**

`src/LogJammer.Api/BackgroundServices/DataRetentionService.cs`:
```csharp
using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.BackgroundServices;

public class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(42); // 6 weeks

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

                var cutoff = DateTimeOffset.UtcNow - RetentionPeriod;

                var deletedOccurrences = await db.PatternOccurrences
                    .Where(o => o.WindowStart < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var deletedPatterns = await db.LogPatterns
                    .Where(p => !p.IsNew && p.LastSeen < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                logger.LogInformation(
                    "Retention cleanup: {Occurrences} occurrences, {Patterns} patterns deleted",
                    deletedOccurrences, deletedPatterns);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data retention cleanup failed");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

- [ ] **Step 3: Register in Program.cs**

Add to `Program.cs`:
```csharp
builder.Services.AddHostedService<BaselineRecalculationService>();
builder.Services.AddHostedService<DataRetentionService>();
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add BaselineRecalculation and DataRetention background services"
```

---

### Task 11b: ElasticsearchPollingService and DataSource test/fields endpoints

**Files:**
- Create: `src/LogJammer.Api/BackgroundServices/ElasticsearchPollingService.cs`
- Modify: `src/LogJammer.Api/Controllers/DataSourcesController.cs`
- Modify: `src/LogJammer.Api/Program.cs`

- [ ] **Step 1: Add test-connection and fields endpoints to DataSourcesController**

Add these actions to the existing `DataSourcesController`:

```csharp
[HttpPost("{id:guid}/test")]
public async Task<IActionResult> Test(Guid id)
{
    var ds = await db.DataSources.FindAsync(id);
    if (ds is null) return NotFound();
    if (ds.Type != DataSourceType.Elasticsearch) return BadRequest("Test only available for Elasticsearch sources");

    try
    {
        var config = JsonSerializer.Deserialize<JsonElement>(ds.ConnectionConfig);
        var url = config.GetProperty("url").GetString()!;
        var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
        var ping = await client.PingAsync();
        return ping.IsValidResponse
            ? Ok(new { success = true })
            : Ok(new { success = false, error = ping.DebugInformation });
    }
    catch (Exception ex)
    {
        return Ok(new { success = false, error = ex.Message });
    }
}

[HttpGet("{id:guid}/fields")]
public async Task<IActionResult> Fields(Guid id)
{
    var ds = await db.DataSources.FindAsync(id);
    if (ds is null) return NotFound();
    if (ds.Type != DataSourceType.Elasticsearch) return BadRequest("Fields only available for Elasticsearch sources");

    var config = JsonSerializer.Deserialize<JsonElement>(ds.ConnectionConfig);
    var url = config.GetProperty("url").GetString()!;
    var indexPattern = config.GetProperty("indexPattern").GetString()!;

    var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
    var response = await client.SearchAsync<JsonElement>(s => s
        .Index(indexPattern)
        .Size(10)
        .Sort(sort => sort.Field("@timestamp", new FieldSort { Order = SortOrder.Desc })));

    if (!response.IsValidResponse)
        return BadRequest(new { error = response.DebugInformation });

    var fields = new Dictionary<string, string?>();
    foreach (var hit in response.Documents)
    {
        foreach (var prop in hit.EnumerateObject())
        {
            if (!fields.ContainsKey(prop.Name))
                fields[prop.Name] = prop.Value.ToString();
        }
    }

    return Ok(fields.Select(f => new FieldInfo(f.Key, f.Value)).ToList());
}
```

Add required usings:
```csharp
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
```

- [ ] **Step 2: Implement ElasticsearchPollingService**

`src/LogJammer.Api/BackgroundServices/ElasticsearchPollingService.cs`:
```csharp
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.BackgroundServices;

public class ElasticsearchPollingService(
    IServiceScopeFactory scopeFactory,
    IngestionPipeline pipeline,
    ILogger<ElasticsearchPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

                var sources = await db.DataSources
                    .Where(d => d.Enabled && d.Type == DataSourceType.Elasticsearch)
                    .ToListAsync(stoppingToken);

                foreach (var ds in sources)
                {
                    try
                    {
                        await PollSourceAsync(ds, db, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to poll Elasticsearch source {DataSourceId}", ds.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Elasticsearch polling cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PollSourceAsync(DataSource ds, LogJammerDbContext db, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<JsonElement>(ds.ConnectionConfig);
        var url = config.GetProperty("url").GetString()!;
        var indexPattern = config.GetProperty("indexPattern").GetString()!;
        var pollingInterval = config.TryGetProperty("pollingIntervalSeconds", out var interval)
            ? interval.GetInt32()
            : 60;

        // Skip if not due for polling
        if (ds.LastPolledAt.HasValue &&
            DateTimeOffset.UtcNow - ds.LastPolledAt.Value < TimeSpan.FromSeconds(pollingInterval))
            return;

        var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
        var since = ds.LastPolledAt ?? DateTimeOffset.UtcNow.AddHours(-1);

        var response = await client.SearchAsync<JsonElement>(s => s
            .Index(indexPattern)
            .Size(1000)
            .Query(q => q
                .Range(r => r
                    .DateRange(dr => dr
                        .Field("@timestamp")
                        .Gt(since.ToString("o")))))
            .Sort(sort => sort.Field("@timestamp", new FieldSort { Order = SortOrder.Asc })), ct);

        if (!response.IsValidResponse)
        {
            logger.LogWarning("ES poll failed for {DataSourceId}: {Info}", ds.Id, response.DebugInformation);
            return;
        }

        if (response.Documents.Count == 0)
        {
            ds.LastPolledAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var entries = response.Documents.Select(doc =>
        {
            var fields = new Dictionary<string, string>();
            foreach (var prop in doc.EnumerateObject())
                fields[prop.Name] = prop.Value.ToString();

            // Auto-detect timestamp
            var timestamp = DateTimeOffset.UtcNow;
            foreach (var tsField in new[] { "@timestamp", "timestamp" })
            {
                if (fields.TryGetValue(tsField, out var tsVal) && DateTimeOffset.TryParse(tsVal, out var parsed))
                {
                    timestamp = parsed;
                    break;
                }
            }

            // Auto-detect level
            string? level = null;
            foreach (var lvlField in new[] { "log.level", "level", "severity" })
            {
                if (fields.TryGetValue(lvlField, out var lvlVal))
                {
                    level = lvlVal;
                    break;
                }
            }

            // Apply message template or fall back to message field
            var message = ds.MessageTemplate is not null
                ? MessageTemplateApplier.Apply(ds.MessageTemplate, StackTracePreprocessor.Process(fields))
                : fields.GetValueOrDefault("message") ?? doc.GetRawText();

            return new RawLogEntry
            {
                Message = message,
                Timestamp = timestamp,
                Level = level
            };
        }).ToList();

        await pipeline.ProcessEntriesAsync(entries, ds.Id, null); // template already applied above

        ds.LastPolledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Polled {Count} entries from ES source {DataSourceId}", entries.Count, ds.Id);
    }
}
```

- [ ] **Step 3: Register in Program.cs**

Add to `Program.cs`:
```csharp
builder.Services.AddHostedService<ElasticsearchPollingService>();
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add ElasticsearchPollingService and test/fields endpoints"
```

---

### Task 12: Docker configuration

**Files:**
- Modify: `src/LogJammer.Api/Dockerfile`
- Modify: `docker-compose.yml`

- [ ] **Step 1: Update Dockerfile**

`src/LogJammer.Api/Dockerfile` (simplified, no frontend yet — will add in frontend task):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .
COPY LogJammer.Engine/LogJammer.Engine.csproj LogJammer.Engine/
COPY LogJammer.Api/LogJammer.Api.csproj LogJammer.Api/
RUN dotnet restore LogJammer.Api/LogJammer.Api.csproj
COPY . .
RUN dotnet publish LogJammer.Api/LogJammer.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LogJammer.Api.dll"]
```

- [ ] **Step 2: Update docker-compose.yml**

```yaml
services:
  db:
    image: postgres:17
    environment:
      POSTGRES_DB: logjammer
      POSTGRES_USER: logjammer
      POSTGRES_PASSWORD: logjammer
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U logjammer -d logjammer"]
      interval: 5s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  api:
    build:
      context: ./src
      dockerfile: LogJammer.Api/Dockerfile
    ports:
      - "5050:8080"
    environment:
      ConnectionStrings__DefaultConnection: "Host=db;Port=5432;Database=logjammer;Username=logjammer;Password=logjammer"
      AUTH__PASSWORD: "changeme"
      AUTH__APIKEY: "changeme"
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 5s
      start_period: 10s
      retries: 3
    restart: unless-stopped

volumes:
  pgdata:
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "chore: update Dockerfile and docker-compose for v2"
```

---

## Chunk 4: Frontend

### Task 13: Scaffold frontend project

**Files:**
- Modify: `src/frontend/` (gut existing, keep Vite config structure)

- [ ] **Step 1: Remove old frontend source files**

```bash
rm -rf src/frontend/src/*
```

- [ ] **Step 2: Install dependencies**

```bash
cd src/frontend && npm install @mui/material@7 @mui/icons-material@7 @emotion/react @emotion/styled @tanstack/react-query@5 react-router-dom@7 chart.js@4 react-chartjs-2
```

Verify `package.json` has React 19, Vite 7, TypeScript 5.9, MUI 7.

- [ ] **Step 3: Create app shell files**

Create these files under `src/frontend/src/`:

- `main.tsx` — React root with providers (QueryClient, Router, Theme)
- `theme.ts` — dark MUI theme (monitoring aesthetic)
- `api/client.ts` — fetch wrapper with auth token handling
- `api/types.ts` — TypeScript mirrors of backend DTOs
- `api/hooks/useAuth.ts` — login mutation, token storage, 401 redirect
- `api/hooks/useDataSources.ts` — CRUD hooks
- `api/hooks/usePatterns.ts` — list, detail, acknowledge hooks
- `api/hooks/useDashboard.ts` — dashboard query hook
- `components/Layout.tsx` — sidebar + topbar + Outlet
- `components/Sidebar.tsx` — nav items (Dashboard, Data Sources)
- `components/TopBar.tsx` — app title
- `components/SeverityChip.tsx` — color-coded severity badge
- `pages/Login.tsx` — password-only login
- `pages/Dashboard.tsx` — stats, new patterns, anomalies
- `pages/DataSources.tsx` — table, CRUD dialogs
- `pages/PatternDetail.tsx` — template, chart, baseline band
- `App.tsx` — router setup

Each file is a standard React component using MUI. The exact implementation follows v1 patterns but with v2's simpler data model.

- [ ] **Step 4: Update vite.config.ts proxy**

Ensure proxy forwards `/api` to `http://localhost:5050`:
```typescript
server: {
  proxy: {
    '/api': 'http://localhost:5050'
  }
}
```

- [ ] **Step 5: Verify dev server starts**

```bash
cd src/frontend && npm run dev
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: scaffold v2 frontend with Login, Dashboard, DataSources, PatternDetail pages"
```

---

### Task 14: Frontend tests

**Files:**
- Create: `src/frontend/src/__tests__/Dashboard.test.tsx`
- Create: `src/frontend/src/__tests__/DataSources.test.tsx`
- Create: `src/frontend/src/__tests__/PatternDetail.test.tsx`

- [ ] **Step 1: Write Dashboard test**

Test that Dashboard renders stats bar, new patterns section, anomalies section. Mock the API hooks.

- [ ] **Step 2: Write DataSources test**

Test that DataSources page renders table, create button opens dialog, delete requires confirmation.

- [ ] **Step 3: Write PatternDetail test**

Test that PatternDetail renders template, sample message, severity, chart placeholder.

- [ ] **Step 4: Run tests**

```bash
cd src/frontend && npm test
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "test: add frontend tests for Dashboard, DataSources, PatternDetail"
```

---

### Task 15: Update Dockerfile for frontend build

**Files:**
- Modify: `src/LogJammer.Api/Dockerfile`
- Modify: `src/LogJammer.Api/Program.cs`

- [ ] **Step 1: Update Dockerfile to 3-stage (frontend + backend + runtime)**

```dockerfile
FROM node:22-alpine AS frontend
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .
COPY LogJammer.Engine/LogJammer.Engine.csproj LogJammer.Engine/
COPY LogJammer.Api/LogJammer.Api.csproj LogJammer.Api/
RUN dotnet restore LogJammer.Api/LogJammer.Api.csproj
COPY . .
RUN dotnet publish LogJammer.Api/LogJammer.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=frontend /app/dist wwwroot/
EXPOSE 8080
ENTRYPOINT ["dotnet", "LogJammer.Api.dll"]
```

- [ ] **Step 2: Add static file serving to Program.cs**

Add before `app.Run()`:
```csharp
app.UseDefaultFiles();
app.UseStaticFiles();
// After all other routes:
app.MapFallbackToFile("index.html");
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build src/LogJammer.slnx
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "chore: add 3-stage Dockerfile with frontend build"
```

---

## Chunk 5: Chrome Extension

### Task 16: Scaffold v2 Chrome extension

**Files:**
- Modify: `src/chrome-extension/` (gut and rebuild from v1 structure)

The Chrome extension is largely carried forward from v1 with these changes:
- Add field selector dialog to subscribe flow
- Per-subscription pause/resume
- Simpler data model (no schema mapping)
- API key auth via `X-Api-Key` header

- [ ] **Step 1: Update shared types**

Update `src/chrome-extension/src/shared/types.ts` to match v2 DTOs (remove v1 schema mapping, fingerprint config references). Add `selectedFields: string[]` to subscription types.

- [ ] **Step 2: Update service worker**

Modify `src/chrome-extension/src/background/service-worker.ts`:
- On subscribe: build MessageTemplate from selected fields, POST to `/api/datasources` with `X-Api-Key` header
- Per-subscription pause/resume (not all-or-nothing)
- On poll: apply message template client-side, combine selected fields from ES hits, POST to `/api/ingest` with `X-Api-Key`

- [ ] **Step 3: Add FieldSelector component**

Create `src/chrome-extension/src/popup/components/FieldSelector.tsx`:
- Checkboxes for each field from captured response
- Up/down ordering buttons
- Live preview of combined message
- Auto-detect timestamp/level fields (pre-checked, labeled)
- Soft warning if >6 fields selected

- [ ] **Step 4: Update SubscribeDialog**

Modify subscribe dialog to include FieldSelector before the polling interval slider.

- [ ] **Step 5: Update Settings tab**

Replace "API token" with "API key" terminology. Send as `X-Api-Key` header.

- [ ] **Step 6: Update content script**

Clean up `kibana-interceptor.ts` — keep fetch patching, improve bsearch parsing, capture response _source fields for the field selector.

- [ ] **Step 7: Run extension tests**

```bash
cd src/chrome-extension && npm test
```

- [ ] **Step 8: Build extension**

```bash
cd src/chrome-extension && npm run build
```

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: update Chrome extension for v2 (field selector, API key auth, per-subscription recovery)"
```

---

## Chunk 6: Integration & Final Verification

### Task 17: End-to-end integration test

**Files:**
- Create: `src/LogJammer.Tests/Integration/EndToEndTests.cs`

- [ ] **Step 1: Write E2E test**

Test the full flow: create data source → ingest entries → verify patterns created → verify dashboard returns data → acknowledge pattern.

```csharp
// Uses Testcontainers PostgreSQL
// Creates an in-memory test server (WebApplicationFactory)
// Exercises: POST /api/datasources, POST /api/ingest, GET /api/patterns, GET /api/dashboard, POST /api/patterns/{id}/acknowledge
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test src/LogJammer.slnx -v normal
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "test: add end-to-end integration test"
```

---

### Task 18: Update specs and documentation

**Files:**
- Modify: `specs/definition-dto.md`
- Modify: `specs/definition-api.md`

- [ ] **Step 1: Update definition-dto.md**

Replace v1 entity definitions with v2: DataSource, DrainState, LogPattern, PatternOccurrence, PatternBaseline, enums.

- [ ] **Step 2: Update definition-api.md**

Replace v1 API endpoints with v2 endpoints.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "docs: update specs for v2 entities and API"
```

---

### Task 19: Update CLAUDE.md

**Files:**
- Modify: `.claude/CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md**

Update to reflect v2 project structure, build commands, and architecture.

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "docs: update CLAUDE.md for v2"
```
