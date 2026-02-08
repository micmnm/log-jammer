using System.Diagnostics;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;

namespace LogJammer.Infrastructure.Adapters.Elasticsearch;

public class ElasticsearchAdapter : IDataSourceAdapter
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchConnectionConfig _config;

    public ElasticsearchAdapter(string connectionConfigJson)
    {
        _config = JsonSerializer.Deserialize<ElasticsearchConnectionConfig>(connectionConfigJson)
            ?? throw new ArgumentException("Invalid Elasticsearch connection config JSON.");

        var settings = new ElasticsearchClientSettings(new Uri(_config.Url));

        if (_config.Auth is not null)
        {
            settings = _config.Auth.Type switch
            {
                "basic" => settings.Authentication(
                    new BasicAuthentication(_config.Auth.Username!, _config.Auth.Password!)),
                "apiKey" => settings.Authentication(
                    new ApiKey(_config.Auth.ApiKey!)),
                _ => settings
            };
        }

        _client = new ElasticsearchClient(settings);
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await _client.PingAsync(cancellationToken);
            sw.Stop();

            if (response.IsValidResponse)
            {
                return new ConnectionTestResult(true, null, sw.Elapsed,
                    new Dictionary<string, object?> { ["indexPattern"] = _config.IndexPattern });
            }

            return new ConnectionTestResult(false,
                response.ElasticsearchServerError?.Error?.Reason ?? "Ping failed",
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(false, ex.Message, sw.Elapsed);
        }
    }

    public async Task<ErrorBatch> PollErrorsAsync(DateTime since, int limit, CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest(_config.IndexPattern)
        {
            Size = limit,
            Query = new Query
            {
                Range = new UntypedRangeQuery("@timestamp")
                {
                    Gte = since.ToString("o")
                }
            },
            Sort = [new SortOptions { Field = new FieldSort("@timestamp") { Order = SortOrder.Desc } }]
        };

        var response = await _client.SearchAsync<JsonDocument>(request, cancellationToken);

        var entries = ParseHits(response);
        var total = (int)response.Total;
        var sampleRatio = total > 0 ? (double)entries.Count / total : 1.0;

        return new ErrorBatch(entries, total, sampleRatio);
    }

    public async Task<IReadOnlyList<RawLogEntry>> GetSampleRecordsAsync(int count, CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest(_config.IndexPattern)
        {
            Size = count,
            Sort = [new SortOptions { Field = new FieldSort("@timestamp") { Order = SortOrder.Desc } }]
        };

        var response = await _client.SearchAsync<JsonDocument>(request, cancellationToken);
        return ParseHits(response);
    }

    public async Task<IReadOnlyList<FieldDefinition>> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var request = new GetMappingRequest(_config.IndexPattern);
        var response = await _client.Indices.GetMappingAsync(request, cancellationToken);

        var fields = new List<FieldDefinition>();
        if (response.IsValidResponse && response.Mappings is not null)
        {
            foreach (var indexMapping in response.Mappings.Values)
            {
                if (indexMapping.Mappings?.Properties is null) continue;
                FlattenProperties(indexMapping.Mappings.Properties, "", fields);
            }
        }

        return fields.DistinctBy(f => f.Name).OrderBy(f => f.Name).ToList();
    }

    private static List<RawLogEntry> ParseHits(SearchResponse<JsonDocument> response)
    {
        var entries = new List<RawLogEntry>();
        if (!response.IsValidResponse || response.Hits is null) return entries;

        foreach (var hit in response.Hits)
        {
            var fields = new Dictionary<string, object?>();
            if (hit.Source is not null)
            {
                var json = JsonSerializer.Serialize(hit.Source);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                if (dict is not null)
                    fields = dict;
            }

            var timestamp = DateTime.UtcNow;
            if (fields.TryGetValue("@timestamp", out var ts) && ts is JsonElement tsEl)
            {
                if (tsEl.TryGetDateTime(out var parsedTs))
                    timestamp = parsedTs;
            }

            entries.Add(new RawLogEntry(timestamp, fields));
        }

        return entries;
    }

    private static void FlattenProperties(Properties properties, string prefix, List<FieldDefinition> fields)
    {
        foreach (var (name, property) in properties)
        {
            var fullName = string.IsNullOrEmpty(prefix) ? name.ToString() : $"{prefix}.{name}";
            var typeName = property.Type ?? "object";

            fields.Add(new FieldDefinition(fullName, typeName, true));

            if (property is ObjectProperty objProp && objProp.Properties is not null)
            {
                FlattenProperties(objProp.Properties, fullName, fields);
            }
        }
    }
}
