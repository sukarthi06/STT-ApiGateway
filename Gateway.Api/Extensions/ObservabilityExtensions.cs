using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Gateway.Api.Extensions;

public static class ObservabilityExtensions
{
    private const string ServiceName = "Gateway.Api";

    public static void AddHostInfrastructure(this IHostBuilder hostBuilder, IConfiguration configuration)
    {
        var otlp = OtlpSettings.From(configuration);

        hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(configuration)   // the "Serilog" section you added
                .ReadFrom.Services(services);

            // No local collector → console/file only
            if (context.HostingEnvironment.IsEnvironment("Local") || otlp is null)
                return;

            loggerConfiguration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlp.Endpoint;
                options.Protocol = otlp.IsHttp ? OtlpProtocol.HttpProtobuf : OtlpProtocol.Grpc;
                options.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = ServiceName };
                if (otlp.Headers is not null)
                    options.Headers = otlp.ParseHeaders();
            });
        });
    }

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var otlp = OtlpSettings.From(configuration);
        if (otlp is null)
            return services;   // no endpoint configured → no export (instead of crashing)

        var protocol = otlp.IsHttp ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()   // gateway → service calls (YARP uses HttpClient)
                .AddOtlpExporter(o => Configure(o, otlp.TracesEndpoint, protocol, otlp.Headers)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => Configure(o, otlp.MetricsEndpoint, protocol, otlp.Headers)));

        return services;
    }

    private static void Configure(OtlpExporterOptions options, string endpoint, OtlpExportProtocol protocol, string? headers)
    {
        options.Endpoint = new Uri(endpoint);
        options.Protocol = protocol;
        if (headers is not null)
            options.Headers = headers;
    }

    // Empty strings count as "not set" (fixes the new Uri("") crash in the reference projects)
    private sealed record OtlpSettings(string Endpoint, string TracesEndpoint, string MetricsEndpoint, bool IsHttp, string? Headers)
    {
        public static OtlpSettings? From(IConfiguration configuration)
        {
            var endpoint = configuration["Otlp:Endpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
                return null;

            return new OtlpSettings(
                endpoint,
                NullIfEmpty(configuration["Otlp:TracesEndpoint"]) ?? endpoint,
                NullIfEmpty(configuration["Otlp:MetricsEndpoint"]) ?? endpoint,
                string.Equals(configuration["Otlp:Protocol"], "http/protobuf", StringComparison.OrdinalIgnoreCase),
                NullIfEmpty(configuration["Otlp:Headers"]));
        }

        public Dictionary<string, string> ParseHeaders() =>
            Headers!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(kv => kv.Split('=', 2))
                .ToDictionary(p => p[0].Trim(), p => Uri.UnescapeDataString(p[1].Trim()));

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}