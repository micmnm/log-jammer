using System.Text.Json.Serialization;

namespace LogJammer.Engine.Data.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataSourceType
{
    KibanaProxy,
    Elasticsearch
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Severity
{
    Info,
    Warning,
    Error,
    Critical
}
