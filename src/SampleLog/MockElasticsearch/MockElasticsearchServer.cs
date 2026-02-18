using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SampleLog.Generation;

namespace SampleLog.MockElasticsearch;

public sealed class MockElasticsearchServer : IAsyncDisposable
{
    private const int Port = 9200;
    private const string IndexName = "sample-logs";
    public static string Url => $"http://localhost:{Port}";
    public static string IndexPattern => IndexName;

    private readonly WebApplication _app;
    private readonly LogGenerator _generator;

    public MockElasticsearchServer(LogGenerator generator)
    {
        _generator = generator;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(Url);
        builder.Logging.ClearProviders(); // keep TUI clean

        _app = builder.Build();

        // ES client requires this header to accept the server as a genuine ES instance
        _app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
            await next();
        });

        MapEndpoints();
    }

    public Task StartAsync() => _app.StartAsync();

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private void MapEndpoints()
    {
        // GET / — Ping / cluster info
        _app.MapGet("/", () => Results.Json(new
        {
            name = "sample-log-mock",
            cluster_name = "samplelog",
            cluster_uuid = "mock-uuid",
            version = new
            {
                number = "8.15.0",
                build_flavor = "default",
                build_type = "docker",
                lucene_version = "9.11.1"
            },
            tagline = "You Know, for Search"
        }));

        // POST /{index}/_search — Search logs
        _app.MapPost("/{index}/_search", async (HttpContext ctx, string index) =>
        {
            var body = await ParseRequestBody(ctx);
            var size = body?["size"]?.GetValue<int>() ?? 10;
            DateTime? gte = null;

            // Parse range query on @timestamp
            var rangeNode = body?["query"]?["range"];
            if (rangeNode is JsonObject rangeObj)
            {
                foreach (var (_, fieldValue) in rangeObj)
                {
                    var gteStr = fieldValue?["gte"]?.GetValue<string>();
                    if (gteStr is not null && DateTime.TryParse(gteStr, out var parsed))
                        gte = parsed;
                }
            }

            var entries = ReadLogEntries(size, gte);
            var hits = entries.Select(e => new { _index = IndexName, _id = Guid.NewGuid().ToString(), _source = e }).ToList();

            return Results.Json(new
            {
                took = 1,
                timed_out = false,
                _shards = new { total = 1, successful = 1, skipped = 0, failed = 0 },
                hits = new
                {
                    total = new { value = hits.Count, relation = "eq" },
                    max_score = (double?)null,
                    hits
                }
            });
        });

        // GET /{index}/_mapping — Field mappings
        _app.MapGet("/{index}/_mapping", (string index) =>
        {
            var properties = new Dictionary<string, object>
            {
                ["@timestamp"] = new { type = "date" },
                ["level"] = new { type = "keyword" },
                ["message"] = new { type = "text" },
                ["service"] = new { type = "keyword" },
                ["exception"] = new { type = "text" },
                ["timestamp"] = new { type = "date" }
            };

            // Add dynamic properties from known templates
            foreach (var template in _generator.Library.Templates)
            {
                if (template.Properties is null) continue;
                foreach (var propName in template.Properties.Keys)
                {
                    if (!properties.ContainsKey(propName))
                    {
                        // Heuristic: if any value is a number, use long; otherwise keyword
                        var values = template.Properties[propName];
                        var isNumeric = values.Any(v => v is JsonElement el && el.ValueKind == JsonValueKind.Number);
                        properties[propName] = new { type = isNumeric ? "long" : "keyword" };
                    }
                }
            }

            var mapping = new Dictionary<string, object>
            {
                [IndexName] = new
                {
                    mappings = new { properties }
                }
            };

            return Results.Json(mapping);
        });
    }

    private List<JsonObject> ReadLogEntries(int count, DateTime? since)
    {
        var entries = new List<JsonObject>();
        var filePath = _generator.JsonFilePath;

        if (!File.Exists(filePath))
            return entries;

        // Read all lines and take the last N (most recent)
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException)
        {
            return entries;
        }

        // Process in reverse order (newest first) to match ES default desc sort
        for (var i = lines.Length - 1; i >= 0 && entries.Count < count; i--)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var obj = JsonNode.Parse(line)?.AsObject();
                if (obj is null) continue;

                // Apply timestamp filter if present
                if (since.HasValue)
                {
                    var tsStr = obj["timestamp"]?.GetValue<string>();
                    if (tsStr is not null && DateTime.TryParse(tsStr, out var ts) && ts < since.Value)
                        continue;
                }

                // Add @timestamp field mirroring timestamp (ES convention)
                var timestamp = obj["timestamp"]?.GetValue<string>();
                if (timestamp is not null && !obj.ContainsKey("@timestamp"))
                    obj["@timestamp"] = timestamp;

                entries.Add(obj);
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        return entries;
    }

    private static async Task<JsonObject?> ParseRequestBody(HttpContext ctx)
    {
        try
        {
            var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            return JsonNode.Parse(doc.RootElement.GetRawText())?.AsObject();
        }
        catch
        {
            return null;
        }
    }
}
