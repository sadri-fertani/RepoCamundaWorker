namespace TestWrkCmd.Common.Options;

public class KafkaOptions
{
    /// <summary>
    /// Comma-separated list of Kafka bootstrap servers (e.g., "localhost:9092"). 
    /// </summary>
    public required string BootstrapServers { get; set; }

    /// <summary>
    /// Consumer group ID for Kafka. Consumers with the same group ID will share message consumption.
    /// </summary>
    public required string GroupId { get; set; }

    /// <summary>
    /// The Kafka topic to subscribe to.
    /// </summary>
    public required string Topic { get; set; }
}