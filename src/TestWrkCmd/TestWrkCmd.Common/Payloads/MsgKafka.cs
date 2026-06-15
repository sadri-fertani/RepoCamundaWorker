namespace TestWrkCmd.Common.Payloads;

public class MsgKafka
{
    public required string ProcessInstanceKey { get; set; }

    public required string Message { get; set; }

    public required string Status { get; set; }
}
