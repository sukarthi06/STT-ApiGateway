using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddGatewayAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = jwt["MetadataAddress"]!;
                options.MapInboundClaims = false;   // keep "sub"/"role" as-is
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("Token validation failed: {Reason}", context.Exception.GetType().Name);
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();   // suppress detailed WWW-Authenticate error
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized")
                            .ExecuteAsync(context.HttpContext);
                    },
                    OnForbidden = context =>
                        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden")
                            .ExecuteAsync(context.HttpContext)
                };
            });

        // Backchannel uses IHttpClientFactory so "authservice" resolves via service discovery
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory>((options, factory) =>
                options.Backchannel = factory.CreateClient("jwt-metadata"));

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}