using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Adapters.Elasticsearch;
using LogJammer.Infrastructure.Adapters.LogFile;
using LogJammer.Infrastructure.Adapters.PostgreSql;

namespace LogJammer.Infrastructure.Adapters;

public class DataSourceAdapterFactory : IDataSourceAdapterFactory
{
    public IDataSourceAdapter CreateAdapter(AdapterType adapterType, string connectionConfig)
    {
        return adapterType switch
        {
            AdapterType.Elasticsearch => new ElasticsearchAdapter(connectionConfig),
            AdapterType.PostgreSql => new PostgreSqlAdapter(connectionConfig),
            AdapterType.LogFile => new LogFileAdapter(connectionConfig),
            _ => throw new ArgumentOutOfRangeException(nameof(adapterType), adapterType, "Unsupported adapter type.")
        };
    }
}
