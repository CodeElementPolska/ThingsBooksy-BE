using System;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceSchema;

internal sealed class DeleteResourceSchemaCommandHandler : ICommandHandler<DeleteResourceSchemaCommand>
{
    private readonly IDeleteResourceSchemaCommandDataProvider _dataProvider;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;

    public DeleteResourceSchemaCommandHandler(
        IDeleteResourceSchemaCommandDataProvider dataProvider,
        IClock clock,
        IMessageBroker messageBroker)
    {
        _dataProvider = dataProvider;
        _clock = clock;
        _messageBroker = messageBroker;
    }

    public async Task HandleAsync(DeleteResourceSchemaCommand command, CancellationToken cancellationToken = default)
    {
        var resourceSchema = await _dataProvider.GetResourceSchemaAsync(command.SchemaId, cancellationToken);

        if (resourceSchema is null)
            throw new ResourcesDomainException("Resource type not found.");

        var group = await _dataProvider.GetGroupAsync(resourceSchema.GroupId, cancellationToken);

        if (group is null || group.OwnerId != command.RequesterId)
            throw new ResourcesForbiddenException("Only the group owner may delete a resource type.");

        var now = _clock.CurrentDate();

        // Cascade soft-delete all instances of this type.
        // ExecuteUpdateAsync is a bulk operation; zero rows affected is a no-op (idempotent).
        await _dataProvider.SoftDeleteInstancesAsync(command.SchemaId, now, cancellationToken);

        // Soft-delete the resource schema; its property definitions stay and are hidden with it.
        resourceSchema.Delete(now);
        await _dataProvider.SaveChangesAsync(cancellationToken);

        // Only the schema event is published; consumers cascade to instances by SchemaId.
        await _messageBroker.PublishAsync(new ResourceSchemaDeletedEvent(resourceSchema.Id, resourceSchema.GroupId), cancellationToken);
    }
}
