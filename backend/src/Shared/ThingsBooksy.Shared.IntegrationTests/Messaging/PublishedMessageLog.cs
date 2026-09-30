using System.Collections.Generic;
using System.Linq;
using ThingsBooksy.Shared.Abstractions.Messaging;

namespace ThingsBooksy.Shared.IntegrationTests.Messaging;

/// <summary>
/// Thread-safe, process-wide log of every message published through <see cref="IMessageBroker"/>
/// while the test host is running. Registered as a singleton by <see cref="ThingsBooksyWebAppFactory"/>
/// and filled by <see cref="RecordingMessageBroker"/>.
///
/// Tests call <see cref="Clear"/> in their Arrange phase and read <see cref="Published"/> /
/// <see cref="OfType{TMessage}"/> in their Assert phase.
/// </summary>
public sealed class PublishedMessageLog
{
    private readonly object _lock = new();
    private readonly List<IMessage> _published = new();

    public IReadOnlyList<IMessage> Published
    {
        get
        {
            lock (_lock)
            {
                return _published.ToList();
            }
        }
    }

    public IReadOnlyList<TMessage> OfType<TMessage>() where TMessage : IMessage
        => Published.OfType<TMessage>().ToList();

    public void Clear()
    {
        lock (_lock)
        {
            _published.Clear();
        }
    }

    internal void Record(IMessage message)
    {
        lock (_lock)
        {
            _published.Add(message);
        }
    }
}
