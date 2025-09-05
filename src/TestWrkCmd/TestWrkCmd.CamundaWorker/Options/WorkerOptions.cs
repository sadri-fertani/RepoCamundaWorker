namespace TestWrkCmd.CamundaWorker.Options;

public class WorkerOptions
{
    /// <summary>
    /// Gets or sets the type of job being processed.
    /// </summary>
    public required string JobType { get; set; }

    /// <summary>
    /// Gets or sets the name of the worker.
    /// </summary>
    public required string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of jobs that can be active simultaneously.
    /// </summary>
    public required int MaxJobActive { get; set; }

    /// <summary>
    /// Gets or sets the interval, in seconds, at which polling operations are performed.
    /// </summary>
    public required int PollInterval { get; set; }

    /// <summary>
    /// Time out in minutes
    /// </summary>
    public required int Timeout { get; set; }
}
