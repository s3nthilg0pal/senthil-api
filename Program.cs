using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using NSwag;
using OpenTelemetry.Metrics;

public partial class Program
{
    private static void Main(string[] args)
    {
        var bld = WebApplication.CreateBuilder();

        bld.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                bld.Configuration.GetConnectionString("Database"));
        });

        bld.Services
            .AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        AgentMetrics.MeterName,
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel")
                    .AddPrometheusExporter();
            });
        
        bld.Services.AddHealthChecks();
        bld.Services.SwaggerDocument(options =>
        {
            options.EnableJWTBearerAuth = false;
            options.DocumentSettings = settings =>
            {
                settings.AddAuth(ApiKeyAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.ApiKey,
                    Name = ApiKeyAuthenticationHandler.HeaderName,
                    In = OpenApiSecurityApiKeyLocation.Header,
                    Description = "Provide your API key in the X-API-Key header."
                });
            };
        }).AddFastEndpoints();
        bld.Services
            .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                _ => { });
        bld.Services.AddAuthorization();
        bld.Services.AddSingleton<AIAgent>(sp =>
            Agent.CreateAgent(sp.GetRequiredService<IConfiguration>()));
        bld.Services.Configure<DeployWebhookOptions>(
            bld.Configuration.GetSection("DeployWebhook"));
        bld.Services.AddSingleton<ILinkCreatedDeploymentQueue, LinkCreatedDeploymentQueue>();
        bld.Services.AddHttpClient<DeployWebhookDispatcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SenthilApi");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        bld.Services.AddHostedService<LinkCreatedDeploymentWorker>();

        var app = bld.Build();

        app.MapHealthChecks("/healthz");
        app.MapPrometheusScrapingEndpoint("/metrics");
        app.UseSwaggerGen();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpoints();
        app.Run();
    }
}
