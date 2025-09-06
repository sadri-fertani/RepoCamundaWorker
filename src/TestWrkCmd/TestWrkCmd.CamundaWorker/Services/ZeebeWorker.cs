using Microsoft.Extensions.Options;
using System.Text.Json;
using TestWrkCmd.CamundaWorker.Options;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace TestWrkCmd.CamundaWorker.Services;

public class ZeebeWorker : BackgroundService
{
    /// <summary>
    /// Represents the Zeebe client used to interact with the Zeebe workflow engine.
    /// </summary>
    private readonly IZeebeClient _zeebeClient;

    /// <summary>
    /// Represents the logger used to log messages and events for the <see cref="ZeebeWorker"/> class.
    /// </summary>
    private readonly ILogger<ZeebeWorker> _logger;

    /// <summary>
    /// Represents the configuration options for connecting to a Zeebe broker.
    /// </summary>
    private readonly ZeebeOptions _zeebeOptions;

    /// <summary>
    /// Represents the configuration options for a worker.
    /// </summary>
    private readonly WorkerOptions _workerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZeebeWorker"/> class, configuring it with the specified logger,
    /// Zeebe options, and worker options.
    /// </summary>
    /// <remarks>This constructor initializes the Zeebe client using the gateway address specified in the
    /// provided <paramref name="zeebeOptions"/>. The client is configured to use plaintext communication.</remarks>
    /// <param name="logger">The logger used to log diagnostic and operational information.</param>
    /// <param name="zeebeOptions">The Zeebe configuration options, including the gateway address.</param>
    /// <param name="workerOptions">The worker configuration options, such as task handling settings.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger"/>, <paramref name="zeebeOptions"/>, or <paramref name="workerOptions"/> is
    /// <see langword="null"/>.</exception>
    public ZeebeWorker
        (
            ILogger<ZeebeWorker> logger,
            IOptions<ZeebeOptions> zeebeOptions,
            IOptions<WorkerOptions> workerOptions
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _zeebeOptions = zeebeOptions?.Value ?? throw new ArgumentNullException(nameof(zeebeOptions));
        _workerOptions = workerOptions?.Value ?? throw new ArgumentNullException(nameof(workerOptions));

        // Initialize the Zeebe client with the provided gateway address
        _zeebeClient = ZeebeClient
            .Builder()
            .UseGatewayAddress(_zeebeOptions.GatewayAddress)
            .UsePlainText() // Pas de TLS via ngrok TCP
            .Build();
    }

    /// <summary>
    /// Executes the background worker process for handling Zeebe jobs.
    /// </summary>
    /// <remarks>This method initializes and starts a Zeebe worker using the configured options, such as job
    /// type,  maximum active jobs, worker name, polling interval, and timeout. The worker processes jobs asynchronously
    /// using the specified job handler. <para> If an exception occurs during the initialization of the worker, it is
    /// logged, and the method returns  a faulted <see cref="Task"/> containing the exception. </para></remarks>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that is triggered when the worker process should stop.</param>
    /// <returns>A completed <see cref="Task"/> if the worker starts successfully; otherwise, a faulted <see cref="Task"/>
    /// containing the exception that occurred during initialization.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _zeebeClient
                .NewWorker()
                .JobType(_workerOptions.JobType)
                .Handler(HandleJobAsync)
                .MaxJobsActive(_workerOptions.MaxJobActive)
                .Name(_workerOptions.WorkerName)
                .PollInterval(TimeSpan.FromSeconds(_workerOptions.PollInterval))
                .Timeout(TimeSpan.FromMinutes(_workerOptions.Timeout))
                .Open();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while starting the Zeebe worker.");

            return Task.FromException(ex);
        }
    }

    /// <summary>
    /// Handles the processing and completion of a job using the specified job client.
    /// </summary>
    /// <remarks>This method logs the job's key and variables, processes the job by creating a result message,
    /// and completes the job by sending the result back to the job system. The job's variables are serialized  into
    /// JSON format before being sent.</remarks>
    /// <param name="client">The job client used to interact with the job system.</param>
    /// <param name="activatedJob">The job to be processed and completed. Must not be null.</param>
    /// <returns>A task that represents the asynchronous operation of handling and completing the job.</returns>
    private async Task HandleJobAsync(IJobClient client, IJob activatedJob)
    {
        _logger.LogInformation("Handling job with key: {Key}", activatedJob.Key);

        var variables = new { result = $"Message from .net at {DateTime.Now}" };
        var variablesJson = JsonSerializer.Serialize(variables);

        await client.NewCompleteJobCommand(activatedJob.Key)
            .Variables(variablesJson)
            .Send();

        _logger.LogInformation("Job variables: {Variables}", activatedJob.Variables);

        _logger.LogInformation("Job {JobKey} completed", activatedJob.Key);
    }
}
