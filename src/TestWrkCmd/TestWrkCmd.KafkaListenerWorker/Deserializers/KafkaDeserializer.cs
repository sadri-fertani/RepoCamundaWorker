using Confluent.Kafka;
using System.Text.Json;

namespace TestWrkCmd.KafkaListenerWorker.Deserializers;

/// <summary>
/// Initializes a new instance of the <see cref="KafkaDeserializer{T}"/> class with the specified logger.
/// </summary>
/// <param name="logger"></param>
/// <exception cref="ArgumentNullException"></exception>
public class KafkaDeserializer<T>(ILogger logger) : IDeserializer<T> where T : class
{
    /// <summary>
    /// Represents the logger used to log messages and events for the <see cref="KafkaDeserializer{T}"/> class.
    /// </summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// A reusable, read-only configuration that allows the JSON serializer to match property names regardless of uppercase or lowercase letters.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Deserializes the specified data into an object of type <typeparamref name="T"/>. 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="isNull"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public T? Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        try
        {
            if (isNull || data.IsEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(data, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            // Log the error or handle it as needed
            _logger.LogError(ex, "JSON deserialization error for topic {Topic}", context.Topic);

            return default;
        }
    }
}