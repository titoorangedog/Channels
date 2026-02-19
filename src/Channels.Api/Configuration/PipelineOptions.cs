namespace Channels.Api.Configuration;

public sealed class PipelineOptions
{
    public int ChannelCapacity { get; set; } = 500;
    public int ConsumerCount { get; set; } = 4;
    public int MaxProcessingRetries { get; set; } = 3;
    public int ShutdownDrainTimeoutSeconds { get; set; } = 20;
    public int ReceiveWaitTimeMs { get; set; } = 2000;
    public int PeekMaxDefault { get; set; } = 100;
    public int ErrorMoveScanLimit { get; set; } = 200;
}
