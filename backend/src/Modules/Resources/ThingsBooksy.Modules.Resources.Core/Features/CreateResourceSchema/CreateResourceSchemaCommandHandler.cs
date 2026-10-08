using System;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceSchema;

internal sealed class CreateResourceSchemaCommandHandler : ICommandHandler<CreateResourceSchemaCommand, Guid>
{
    private readonly ICreateResourceSchemaCommandDataProvider _dataProvider;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;

    public CreateResourceSchemaCommandHandler(
        ICreateResourceSchemaCommandDataProvider dataProvider,
        IClock clock,
        IMessageBroker messageBroker)
    {
        _dataProvider = dataProvider;
        _clock = clock;
        _messageBroker = messageBroker;
    }

    public async Task<Guid> HandleAsync(CreateResourceSchemaCommand command, CancellationToken cancellationToken = default)
    {
        var group = await _dataProvider.GetGroupAsync(command.GroupId, cancellationToken);

        if (group is null)
            throw new ResourcesDomainException("Group not found.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ResourcesDomainException("Resource schema name cannot be empty.");

        if (group.OwnerId != command.CallerId)
            throw new ResourcesForbiddenException("Only the group owner may create a resource schema.");

        var normalizedName = command.Name.Trim();
        var nameExists = await _dataProvider.ExistsByGroupAndNameAsync(command.GroupId, normalizedName, excludeId: null, cancellationToken);

        if (nameExists)
            throw new ResourceSchemaNameAlreadyExistsException(command.GroupId, normalizedName);

        var resourceSchema = ResourceSchema.Create(command, _clock.CurrentDate());

        foreach (var def in command.PropertyDefinitions)
        {
            var definition = ResourcePropertyDefinition.Create(def, resourceSchema.Id);
            await _dataProvider.AddPropertyDefinitionAsync(definition, cancellationToken);
        }

        await _dataProvider.AddResourceSchemaAsync(resourceSchema, cancellationToken);
        await _dataProvider.SaveChangesAsync(cancellationToken);
        await _messageBroker.PublishAsync(new ResourceSchemaCreatedEvent(resourceSchema.Id, resourceSchema.GroupId), cancellationToken);

        return resourceSchema.Id;
    }
}
