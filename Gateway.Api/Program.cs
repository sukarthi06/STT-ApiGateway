using Gateway.Api.Extensions;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddGatewayKeyVault();
builder.AddServiceDefaults();

builder.Host.AddHostInfrastructure(builder.Configuration);
if (!builder.Environment.IsEnvironment("Local"))
{
    builder.Services.AddObservability(builder.Configuration);
}

builder.Services.AddGatewayAuth(builder.Configuration); 
builder.Services.AddGatewayProxy(builder.Configuration);
builder.Services.AddGatewayCors(builder.Configuration);

var app = builder.Build();

app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.MapScalarApiReference(options =>
    {
        options.AddDocument("auth", "Auth Service", "/docs/auth/openapi/v1.json");
        options.AddDocument("recordings", "Recording Service", "/docs/recordings/openapi/v1.json");

        options.Servers = [new ScalarServer("/")];   // Try-it goes via the gateway
    }).AllowAnonymous();
}

app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

app.MapReverseProxy();

app.Run();