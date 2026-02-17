using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Adapters.Elasticsearch;

namespace LogJammer.Api.Services;

public class DataSourceService(
    IDataSourceRepository repository,
    IDataSourceAdapterFactory adapterFactory) : IDataSourceService
{
    public async Task<IReadOnlyList<DataSourceResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var dataSources = await repository.GetAllAsync(cancellationToken);
        return dataSources.Select(MapToResponse).ToList();
    }

    public async Task<DataSourceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await repository.GetByIdAsync(id, cancellationToken);
        return dataSource is null ? null : MapToResponse(dataSource);
    }

    public async Task<DataSourceResponse> CreateAsync(CreateDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        var dataSource = new DataSource
        {
            Name = request.Name,
            AdapterType = request.AdapterType,
            ConnectionConfig = request.ConnectionConfig,
            PollIntervalSeconds = request.PollIntervalSeconds,
            SchemaMapping = request.SchemaMapping,
            SamplingBudget = request.SamplingBudget,
            Enabled = request.Enabled
        };

        var created = await repository.AddAsync(dataSource, cancellationToken);
        return MapToResponse(created);
    }

    public async Task<DataSourceResponse?> UpdateAsync(Guid id, UpdateDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        var dataSource = await repository.GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return null;

        if (request.Name is not null) dataSource.Name = request.Name;
        if (request.AdapterType is not null) dataSource.AdapterType = request.AdapterType.Value;
        if (request.ConnectionConfig is not null) dataSource.ConnectionConfig = request.ConnectionConfig;
        if (request.PollIntervalSeconds is not null) dataSource.PollIntervalSeconds = request.PollIntervalSeconds.Value;
        if (request.SchemaMapping is not null) dataSource.SchemaMapping = request.SchemaMapping;
        if (request.SamplingBudget is not null) dataSource.SamplingBudget = request.SamplingBudget.Value;
        if (request.Enabled is not null) dataSource.Enabled = request.Enabled.Value;

        await repository.UpdateAsync(dataSource, cancellationToken);
        return MapToResponse(dataSource);
    }

    public async Task<bool> DeleteAsync(Guid id, bool preserveHistory = false, CancellationToken cancellationToken = default)
    {
        var dataSource = await repository.GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return false;

        if (preserveHistory)
            await repository.DetachKnownErrorsAsync(id, cancellationToken);

        await repository.DeleteAsync(dataSource, cancellationToken);
        return true;
    }

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.ExistsAsync(id, cancellationToken))
            return null;

        var impact = await repository.GetDeletionImpactAsync(id, cancellationToken);

        return new DeletionImpactResponse
        {
            ErrorGroupCount = impact.ErrorGroupCount,
            OccurrenceCount = impact.OccurrenceCount,
            AlertCount = impact.AlertCount,
            ClassificationQueueCount = impact.ClassificationQueueCount,
            TagCount = impact.TagCount,
            RuleCount = impact.RuleCount
        };
    }

    public async Task<ConnectionTestResponse?> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await repository.GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return null;

        var adapter = adapterFactory.CreateAdapter(dataSource.AdapterType, dataSource.ConnectionConfig);
        var result = await adapter.TestConnectionAsync(cancellationToken);

        return new ConnectionTestResponse
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            LatencyMs = result.Latency.TotalMilliseconds,
            Metadata = result.Metadata
        };
    }

    public async Task<SchemaResponse?> GetSchemaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await repository.GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return null;

        var adapter = adapterFactory.CreateAdapter(dataSource.AdapterType, dataSource.ConnectionConfig);
        var fields = await adapter.GetSchemaAsync(cancellationToken);

        return new SchemaResponse
        {
            Fields = fields.Select(f => new FieldDefinitionDto
            {
                Name = f.Name,
                Type = f.Type,
                IsNullable = f.IsNullable
            }).ToList()
        };
    }

    public async Task<SampleRecordsResponse?> GetSampleRecordsAsync(Guid id, int count, CancellationToken cancellationToken = default)
    {
        var dataSource = await repository.GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return null;

        var adapter = adapterFactory.CreateAdapter(dataSource.AdapterType, dataSource.ConnectionConfig);
        var records = await adapter.GetSampleRecordsAsync(count, cancellationToken);

        return new SampleRecordsResponse
        {
            Records = records.Select(r => new RawLogEntryDto
            {
                Timestamp = r.Timestamp,
                Fields = r.Fields
            }).ToList()
        };
    }

    public async Task<DiscoverIndicesResponse> DiscoverIndicesAsync(DiscoverIndicesRequest request, CancellationToken cancellationToken = default)
    {
        var adapter = (ElasticsearchAdapter)adapterFactory.CreateAdapter(
            AdapterType.Elasticsearch, request.ConnectionConfig);

        var (aliases, dataStreams, concreteIndices) = await adapter.DiscoverIndicesAsync(
            request.ShowConcreteIndices, cancellationToken);

        return new DiscoverIndicesResponse
        {
            Aliases = aliases.Select(a => new AliasInfo
            {
                Name = a.Alias,
                Indices = a.Indices
            }).ToList(),
            DataStreams = dataStreams.Select(ds => new DataStreamInfo
            {
                Name = ds.Name,
                BackingIndices = ds.BackingIndices
            }).ToList(),
            ConcreteIndices = concreteIndices
        };
    }

    public async Task<SchemaResponse> DiscoverSchemaAsync(DiscoverSchemaRequest request, CancellationToken cancellationToken = default)
    {
        var adapter = adapterFactory.CreateAdapter(AdapterType.Elasticsearch, request.ConnectionConfig);
        var fields = await adapter.GetSchemaAsync(cancellationToken);

        return new SchemaResponse
        {
            Fields = fields.Select(f => new FieldDefinitionDto
            {
                Name = f.Name,
                Type = f.Type,
                IsNullable = f.IsNullable
            }).ToList()
        };
    }

    private static DataSourceResponse MapToResponse(DataSource ds) => new()
    {
        Id = ds.Id,
        Name = ds.Name,
        AdapterType = ds.AdapterType,
        ConnectionConfig = ds.ConnectionConfig,
        PollIntervalSeconds = ds.PollIntervalSeconds,
        SchemaMapping = ds.SchemaMapping,
        SamplingBudget = ds.SamplingBudget,
        Enabled = ds.Enabled,
        CreatedAt = ds.CreatedAt,
        UpdatedAt = ds.UpdatedAt,
        FingerprintConfigs = ds.FingerprintConfigs?.Select(fc => new FingerprintConfigResponse
        {
            Id = fc.Id,
            DataSourceId = fc.DataSourceId,
            FieldName = fc.FieldName,
            Order = fc.Order,
            NormalizeBeforeHash = fc.NormalizeBeforeHash,
            CreatedAt = fc.CreatedAt
        }).ToList() ?? []
    };
}
