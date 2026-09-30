using System;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceType;

internal sealed class DeleteResourceTypeCommandHandler : ICommandHandler<DeleteResourceTypeCommand>
{
    private readonly IDeleteResourceTypeCommandDataProvider _dataProvider;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;

    public DeleteResourceTypeCommandHandler(
        IDeleteResourceTypeCommandDataProvider dataProvider,
        IClock clock,
        IMessageBroker messageBroker)
    {
        _dataProvider = dataProvider;
        _clock = clock;
        _messageBroker = messageBroker;
    }

    public async Task HandleAsync(DeleteResourceTypeCommand command, CancellationToken cancellationToken = default)
    {
        var resourceType = await _dataProvider.GetResourceTypeAsync(command.TypeId, cancellationToken);

        if (resourceType is null)
            throw new ResourcesDomainException("Resource type not found.");

        var group = await _dataProvider.GetGroupAsync(resourceType.GroupId, cancellationToken);

        if (group is null || group.OwnerId != command.RequesterId)
            throw new ResourcesForbiddenException("Only the group owner may delete a resource type.");

        var now = _clock.CurrentDate();

        // Cascade soft-delete all instances of this type.
        // ExecuteUpdateAsync is a bulk operation; zero rows affected is a no-op (idempotent).
        await _dataProvider.SoftDeleteInstancesAsync(command.TypeId, now, cancellationToken);

        // Soft-delete the resource type; its property definitions stay and are hidden with it.
        resourceType.Delete(now);
        await _dataProvider.SaveChangesAsync(cancellationToken);

        // Only the schema event is published; consumers cascade to instances by SchemaId.
        await _messageBroker.PublishAsync(new ResourceSchemaDeletedEvent(resourceType.Id, resourceType.GroupId), cancellationToken);
    }
}
