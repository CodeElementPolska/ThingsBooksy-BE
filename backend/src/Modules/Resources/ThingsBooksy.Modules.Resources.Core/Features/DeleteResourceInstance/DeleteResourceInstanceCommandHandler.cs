using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceInstance;

internal sealed class DeleteResourceInstanceCommandHandler : ICommandHandler<DeleteResourceInstanceCommand>
{
    private readonly IDeleteResourceInstanceCommandDataProvider _dataProvider;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;

    public DeleteResourceInstanceCommandHandler(
        IDeleteResourceInstanceCommandDataProvider dataProvider,
        IClock clock,
        IMessageBroker messageBroker)
    {
        _dataProvider = dataProvider;
        _clock = clock;
        _messageBroker = messageBroker;
    }

    public async Task HandleAsync(DeleteResourceInstanceCommand command, CancellationToken cancellationToken = default)
    {
        var instance = await _dataProvider.GetInstanceAsync(command.InstanceId, cancellationToken);

        if (instance is null)
            throw new ResourcesDomainException("Resource instance not found.");

        var isOwner = await _dataProvider.IsOwnerAsync(instance.GroupId, command.RequesterId, cancellationToken);

        if (!isOwner)
            throw new ResourcesForbiddenException("Only the group owner may delete a resource instance.");

        instance.Delete(_clock.CurrentDate());

        await _dataProvider.SaveChangesAsync(cancellationToken);
        await _messageBroker.PublishAsync(new ResourceInstanceDeletedEvent(instance.Id, instance.ResourceSchemaId), cancellationToken);
    }
}
