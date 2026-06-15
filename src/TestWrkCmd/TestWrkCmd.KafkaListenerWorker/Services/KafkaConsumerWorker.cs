using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TestWrkCmd.Common.Options;
using TestWrkCmd.Common.Payloads;
using TestWrkCmd.KafkaListenerWorker.Deserializers;
using Zeebe.Client;

namespace TestWrkCmd.KafkaListenerWorker.Services;

public class KafkaConsumerWorker : BackgroundService
{
    /// <summary>
    /// Represents the Zeebe client used to interact with the Zeebe workflow engine.
    /// </summary>
    private readonly IZeebeClient _zeebeClient;

    /// <summary>
    /// Represents the Kafka consumer used to consume messages from a Kafka topic.
    /// </summary>
    private readonly IConsumer<string, MsgKafka> _consumer;

    /// <summary>
    /// Represents the logger used to log messages and events for the <see cref="KafkaConsumerWorker"/> class.
    /// </summary>
    private readonly ILogger<KafkaConsumerWorker> _logger;

    /// <summary>
    /// Represents the configuration options for connecting to a Zeebe broker.
    /// </summary>
    private readonly ZeebeOptions _zeebeOptions;

    /// <summary>
    /// Represents the configuration options for connecting to a Kafka broker.
    /// </summary>
    private readonly KafkaOptions _kafkaOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerWorker"/> class with the specified dependencies.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="zeebeOptions"></param>
    /// <param name="kafkaOptions"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public KafkaConsumerWorker
        (
            ILogger<KafkaConsumerWorker> logger,
            IOptions<ZeebeOptions> zeebeOptions,
            IOptions<KafkaOptions> kafkaOptions
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _zeebeOptions = zeebeOptions?.Value ?? throw new ArgumentNullException(nameof(zeebeOptions));
        _kafkaOptions = kafkaOptions?.Value ?? throw new ArgumentNullException(nameof(kafkaOptions));

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // IMPORTANT
            AllowAutoCreateTopics = false
        };

        _consumer = new ConsumerBuilder<string, MsgKafka>(config)
            .SetValueDeserializer(new KafkaDeserializer<MsgKafka>(_logger))
            .Build();

        _consumer.Subscribe(_kafkaOptions.Topic);

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
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that is triggered when the worker process should stop.</param>
    /// <returns>A completed <see cref="Task"/> if the worker starts successfully; otherwise, a faulted <see cref="Task"/>
    /// containing the exception that occurred during initialization.</returns>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Listener Kafka worker started");

                var result = _consumer.Consume(cancellationToken);

                _logger.LogInformation("Message received: {Value}", result.Message.Value);

                // Traitement métier
                await HandleMessageAsync(result.Message.Value, cancellationToken);

                // Commit manuel
                _consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                // This exception is expected when the cancellation token is triggered, so we can ignore it.
                _logger.LogWarning("Operation canceled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred.");
            }
        }

        _consumer.Close();
        _logger.LogInformation("ListenerKafka worker stopped");
    }

    /// <summary>
    /// Handles the processing of a Kafka message by creating and completing a Zeebe job based on the message content.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    private async Task HandleMessageAsync(MsgKafka? msg, CancellationToken cancellationToken)
    {
        // Vérification de la validité du payload
        if (msg == null)
        {
            _logger.LogWarning("Received null payload, skipping processing.");
            return;
        }

        var targetCamundaMessage = GetMessageName(msg.Status);

        if (targetCamundaMessage == null)
        {
            _logger.LogWarning("Unknown status: {Status}", msg.Status);
        }
        else
        {
            await _zeebeClient
                   .NewPublishMessageCommand()
                   .MessageName(targetCamundaMessage)
                   .CorrelationKey(msg.ProcessInstanceKey)
                   .Variables(JsonSerializer.Serialize(new { processInstanceKey = msg.ProcessInstanceKey }))
                   .Send(cancellationToken);
        }
    }

    /// <summary>
    /// Maps a given status string to a corresponding message name used for publishing messages to the Zeebe workflow engine. 
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    private static string? GetMessageName(string status)
    {
        return status switch
        {
            "Archived" => "msg_archived",
            "Produced" => "msg_produced",
            _ => null
        };
    }
}
