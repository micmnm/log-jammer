using Elastic.CommonSchema;
using Elastic.CommonSchema.Serilog;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting;

namespace LogJammer.Api;

/// <summary>
/// Logs: one ECS JSON object per line on standard output, for collection.
/// Traces and runtime metrics: OTLP over HTTP (or gRPC with OTEL_EXPORTER_OTLP_PROTOCOL=grpc), only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
/// </summary>
public static class Observability
{
    public const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static bool TracingEnabled =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EndpointVariable));

    /// <summary>OTLP exporter headers for a bearer token; null (no header) when the token is empty.</summary>
    public static string? AuthHeaders(string? token) =>
        string.IsNullOrEmpty(token) ? null : $"Authorization=Bearer {token}";

    /// <summary>OTLP protocol for OTEL_EXPORTER_OTLP_PROTOCOL: "grpc" (any casing) selects gRPC; anything else keeps HTTP/protobuf.</summary>
    public static OtlpExportProtocol ProtocolFor(string? value) =>
        value?.Trim().Equals("grpc", StringComparison.OrdinalIgnoreCase) == true
            ? OtlpExportProtocol.Grpc
            : OtlpExportProtocol.HttpProtobuf;

    /// <summary>Whether a request path is traced: false for the paths in <paramref name="untracedPaths"/> (segment match, any casing).</summary>
    public static bool IsTraced(PathString path, string[] untracedPaths) =>
        !untracedPaths.Any(p => path.StartsWithSegments(p));

    public static ITextFormatter CreateLogFormatter(string serviceName, string? environment) =>
        new EcsTextFormatter(new EcsTextFormatterConfiguration
        {
            MapCustom = (doc, _) =>
            {
                doc.Service = new Service { Name = serviceName, Environment = environment };
                return doc;
            }
        });

    /// <summary>LOG_FORMAT=text keeps readable lines for local work; anything else gets ECS JSON.</summary>
    public static LoggerConfiguration WriteToConsole(this LoggerConfiguration configuration, string serviceName) =>
        Environment.GetEnvironmentVariable("LOG_FORMAT") == "text"
            ? configuration.WriteTo.Console()
            : configuration.WriteTo.Console(
                CreateLogFormatter(serviceName, Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT")));

    public static WebApplicationBuilder AddObservability(
        this WebApplicationBuilder builder, string defaultServiceName, params string[] untracedPaths)
    {
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? defaultServiceName;
        var environment = Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT");

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteToConsole(serviceName));

        if (!TracingEnabled)
            return builder;

        void ConfigureOtlp(OtlpExporterOptions options)
        {
            // The endpoint comes from OTEL_EXPORTER_OTLP_ENDPOINT: used as given with gRPC;
            // with HTTP the SDK appends /v1/traces and /v1/metrics.
            options.Protocol = ProtocolFor(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL"));
            options.TimeoutMilliseconds = 3000;
            options.Headers = AuthHeaders(Environment.GetEnvironmentVariable("ELASTIC_APM_SECRET_TOKEN"));
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName);
                if (!string.IsNullOrEmpty(environment))
                    resource.AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment)]);
            })
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = http => IsTraced(http.Request.Path, untracedPaths);
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(ConfigureOtlp))
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(ConfigureOtlp));

        return builder;
    }
}
