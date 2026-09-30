using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Shared.Abstractions.Messaging;

namespace ThingsBooksy.Shared.IntegrationTests.Messaging;

/// <summary>
/// Test-only <see cref="IMessageBroker"/> decorator: records every published message in the
/// shared <see cref="PublishedMessageLog"/> and then forwards it to the original broker, so the
/// application keeps its real in-process messaging behaviour.
/// </summary>
public sealed class RecordingMessageBroker : IMessageBroker
{
    private readonly IMessageBroker _inner;
    private readonly PublishedMessageLog _log;

    public RecordingMessageBroker(IMessageBroker inner, PublishedMessageLog log)
    {
        _inner = inner;
        _log = log;
    }

    public Task PublishAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        _log.Record(message);
        return _inner.PublishAsync(message, cancellationToken);
    }

    public Task PublishAsync(IMessage[] messages, CancellationToken cancellationToken = default)
    {
        if (messages is not null)
        {
            foreach (var message in messages)
            {
                if (message is not null)
                    _log.Record(message);
            }
        }

        return _inner.PublishAsync(messages!, cancellationToken);
    }
}
