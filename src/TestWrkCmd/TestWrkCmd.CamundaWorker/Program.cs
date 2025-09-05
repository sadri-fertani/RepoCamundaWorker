using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using TestWrkCmd.CamundaWorker.Options;
using TestWrkCmd.CamundaWorker.Services;

// Create the host builder
var builder = Host.CreateDefaultBuilder(args);

// Build configuration
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Configure the host
var host = builder
    .ConfigureAppConfiguration((context, config) =>
    {
        // Add configuration sources
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Register health checks
        services
            .AddHealthChecks();

        // Register Zeebe worker and configure options
        services
            .AddHostedService<ZeebeWorker>()
            .Configure<ZeebeOptions>(configuration.GetSection("Zeebe"))
            .Configure<WorkerOptions>(configuration.GetSection("Worker"));
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        // Set the URLs for the web host from configuration
        webBuilder.UseUrls(configuration["Urls"]!);

        webBuilder.Configure(app =>
        {
            // Enable routing
            app.UseRouting();

            // Configure health check endpoints
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");

                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("self") });
            });
        });
    })
    .Build();

// Start the worker
await host.RunAsync();