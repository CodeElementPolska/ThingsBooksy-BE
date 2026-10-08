using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;

internal sealed class CreateResourceInstanceCommandHandler : ICommandHandler<CreateResourceInstanceCommand, Guid>
{
    private readonly ICreateResourceInstanceCommandDataProvider _dataProvider;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;

    public CreateResourceInstanceCommandHandler(
        ICreateResourceInstanceCommandDataProvider dataProvider,
        IClock clock,
        IMessageBroker messageBroker)
    {
        _dataProvider = dataProvider;
        _clock = clock;
        _messageBroker = messageBroker;
    }

    public async Task<Guid> HandleAsync(CreateResourceInstanceCommand command, CancellationToken cancellationToken = default)
    {
        var resourceSchema = await _dataProvider.GetResourceSchemaAsync(command.ResourceSchemaId, cancellationToken);

        if (resourceSchema is null)
            throw new ResourcesDomainException("Resource schema not found.");

        var group = await _dataProvider.GetGroupAsync(resourceSchema.GroupId, cancellationToken);

        if (group is null)
            throw new ResourcesDomainException("Group not found.");

        if (group.OwnerId != command.CallerId)
            throw new ResourcesForbiddenException("Only the group owner may create a resource instance.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ResourcesDomainException("Instance name cannot be empty.");

        var nameExists = await _dataProvider.NameExistsAsync(command.ResourceSchemaId, command.Name, cancellationToken);

        if (nameExists)
            throw new ResourcesDomainException("Instance name is already taken.");

        var propertyValuesList = (command.PropertyValues ?? []).ToList();

        var definitions = await _dataProvider.GetPropertyDefinitionsAsync(command.ResourceSchemaId, cancellationToken);

        var submittedIds = propertyValuesList.Select(pv => pv.PropertyDefinitionId).ToHashSet();

        var invalidRef = propertyValuesList.FirstOrDefault(pv => definitions.All(d => d.Id != pv.PropertyDefinitionId));
        if (invalidRef is not null)
            throw new ResourcesDomainException("Invalid property definition reference.");

        var missingRequired = definitions.FirstOrDefault(d => d.IsRequired && !submittedIds.Contains(d.Id));
        if (missingRequired is not null)
            throw new ResourcesDomainException($"Required property '{missingRequired.Name}' is missing.");

        foreach (var pv in propertyValuesList)
        {
            var def = definitions.First(d => d.Id == pv.PropertyDefinitionId);
            if (def.DataType == PropertyDataType.Number && !decimal.TryParse(pv.Value, out _))
                throw new ResourcesDomainException($"Property '{def.Name}' expects a numeric value.");
            if (def.DataType == PropertyDataType.Boolean && !bool.TryParse(pv.Value, out _))
                throw new ResourcesDomainException($"Property '{def.Name}' expects a boolean value (true/false).");
        }

        var instance = ResourceInstance.Create(command, resourceSchema.GroupId, _clock.CurrentDate());

        foreach (var pv in propertyValuesList)
        {
            var propertyValue = ResourcePropertyValue.Create(pv, instance.Id);

            await _dataProvider.AddPropertyValueAsync(propertyValue, cancellationToken);
        }

        await _dataProvider.AddResourceInstanceAsync(instance, cancellationToken);
        await _dataProvider.SaveChangesAsync(cancellationToken);
        await _messageBroker.PublishAsync(new ResourceInstanceCreatedEvent(instance.Id, instance.ResourceSchemaId), cancellationToken);

        return instance.Id;
    }
}
