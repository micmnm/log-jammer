using LogJammer.Core.Enums;

namespace LogJammer.Core.Interfaces;

public interface IDataSourceAdapterFactory
{
    IDataSourceAdapter CreateAdapter(AdapterType adapterType, string connectionConfig);
}
