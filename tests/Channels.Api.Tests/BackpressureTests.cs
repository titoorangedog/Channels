using System.Diagnostics;
using System.Threading.Channels;
using Channels.Api.Contracts;

namespace Channels.Api.Tests;

public sealed class BackpressureTests
{
    [Fact]
    public async Task BoundedChannel_ShouldApplyBackpressure()
    {
        var channel = Channel.CreateBounded<QueueReceiveItem>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var first = new QueueReceiveItem { MessageId = "1", Body = "{}", EnqueuedAt = DateTimeOffset.UtcNow };
        var second = new QueueReceiveItem { MessageId = "2", Body = "{}", EnqueuedAt = DateTimeOffset.UtcNow };

        await channel.Writer.WriteAsync(first);

        var sw = Stopwatch.StartNew();
        var writeTask = channel.Writer.WriteAsync(second).AsTask();

        await Task.Delay(150);
        Assert.False(writeTask.IsCompleted);

        _ = await channel.Reader.ReadAsync();
        await writeTask;
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 120);
    }
}
