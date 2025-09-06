using System.Text.Json.Serialization;

namespace TestWrkCmd.CamundaWorker.Payloads;

public class MonApiPayload
{
    [JsonPropertyName("HOSTNAME")]

    public string? Hostname { get; set; }

    [JsonPropertyName("applicationName")]

    public string? ApplicationName { get; set; }
}
