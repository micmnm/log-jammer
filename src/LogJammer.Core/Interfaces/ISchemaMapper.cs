using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface ISchemaMapper
{
    MappedLogEntry Map(RawLogEntry entry, string? schemaMappingJson);
}
