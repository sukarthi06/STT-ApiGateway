using Gateway.Api.Options;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms;

namespace Gateway.Api.Extensions;

public static class ProxyExtensions
{
    private const string UserIdHeader = "X-User-Id";
    private const string GatewaySecretHeader = "X-Gateway-Secret";

    public static IServiceCollection AddGatewayProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddServiceDiscoveryDestinationResolver()
            .AddTransforms(context =>
            {
                var gatewayAuth = context.Services.GetRequiredService<IOptionsMonitor<GatewayAuthOptions>>();

                context.AddRequestTransform(transformContext =>
                {
                    var headers = transformContext.ProxyRequest.Headers;

                    // never trust client-sent values
                    headers.Remove(UserIdHeader);
                    headers.Remove(GatewaySecretHeader);

                    headers.Add(GatewaySecretHeader, gatewayAuth.CurrentValue.Secret);

                    var userId = transformContext.HttpContext.User.FindFirst("sub")?.Value;
                    if (userId is not null)
                        headers.Add(UserIdHeader, userId);

                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }
}