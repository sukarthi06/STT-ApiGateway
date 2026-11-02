namespace Gateway.Api.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "AllowedClientOrigins";

    public static IServiceCollection AddGatewayCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();   // lets the browser send/receive the refresh cookie cross-origin
            });
        });

        return services;
    }
}