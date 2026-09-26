using System.Threading.Channels;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public sealed class ChannelAnalysisQueue : IAnalysisQueue
{
    private readonly Channel<AnalysisJob> _channel;

    public ChannelAnalysisQueue(IOptions<ScalingOptions> options) =>
        _channel = Channel.CreateBounded<AnalysisJob>(new BoundedChannelOptions(Math.Max(1, options.Value.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(AnalysisJob job, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public ValueTask<AnalysisJob> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
