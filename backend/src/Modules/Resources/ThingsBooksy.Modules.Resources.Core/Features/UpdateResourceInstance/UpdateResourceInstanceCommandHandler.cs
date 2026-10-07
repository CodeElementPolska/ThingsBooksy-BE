using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceInstance;

internal sealed class UpdateResourceInstanceCommandHandler : ICommandHandler<UpdateResourceInstanceCommand>
{
    private readonly IUpdateResourceInstanceCommandDataProvider _dataProvider;
    private readonly IClock _clock;

    public UpdateResourceInstanceCommandHandler(IUpdateResourceInstanceCommandDataProvider dataProvider, IClock clock)
    {
        _dataProvider = dataProvider;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateResourceInstanceCommand command, CancellationToken cancellationToken = default)
    {
        var instance = await _dataProvider.GetInstanceWithValuesAsync(command.InstanceId, cancellationToken);

        if (instance is null)
            throw new ResourcesDomainException("Resource instance not found.");

        var isOwner = await _dataProvider.IsOwnerAsync(instance.GroupId, command.RequesterId, cancellationToken);

        if (!isOwner)
            throw new ResourcesForbiddenException("Only the group owner may update a resource instance.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ResourcesDomainException("Instance name cannot be empty.");

        var propertyValuesList = (command.PropertyValues ?? []).ToList();

        var definitions = await _dataProvider.GetPropertyDefinitionsAsync(instance.ResourceSchemaId, cancellationToken);

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

        _dataProvider.RemovePropertyValues(instance.PropertyValues);

        instance.Update(command, _clock.CurrentDate());

        foreach (var pv in propertyValuesList)
        {
            var propertyValue = ResourcePropertyValue.Create(pv, instance.Id);

            await _dataProvider.AddPropertyValueAsync(propertyValue, cancellationToken);
        }

        await _dataProvider.SaveChangesAsync(cancellationToken);
    }
}
