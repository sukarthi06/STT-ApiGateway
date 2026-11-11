using Gateway.Api.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddGatewayKeyVault();
builder.AddServiceDefaults();
builder.Services.AddGatewayAuth(builder.Configuration); 
builder.Services.AddGatewayProxy(builder.Configuration);
builder.Services.AddGatewayCors(builder.Configuration);

var app = builder.Build();

app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.MapScalarApiReference(options =>
    {
        options.AddDocument("auth", "Auth Service", "/docs/auth/openapi/v1.json");
        options.AddDocument("recordings", "Recording Service", "/docs/recordings/openapi/v1.json");

        options.Servers = [new ScalarServer("/")];   // Try-it goes via the gateway
    }).AllowAnonymous();
}

app.MapGet("/health", (ILogger<Program> logger) =>
{
    logger.LogInformation("Health check hit");
    return Results.Ok();
}).AllowAnonymous();

app.MapReverseProxy();

app.Run();