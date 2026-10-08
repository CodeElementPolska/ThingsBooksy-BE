using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstance.Models;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstances;

internal sealed class GetResourceInstancesQueryHandler : IQueryHandler<GetResourceInstancesQuery, GetResourceInstancesQueryResult>
{
    private const int MinTake = 1;
    private const int MaxTake = 50;
    private const int DefaultTake = 20;

    private readonly IGetResourceInstancesQueryDataProvider _dataProvider;

    public GetResourceInstancesQueryHandler(IGetResourceInstancesQueryDataProvider dataProvider)
        => _dataProvider = dataProvider;

    public async Task<GetResourceInstancesQueryResult> HandleAsync(GetResourceInstancesQuery query, CancellationToken cancellationToken = default)
    {
        Guid resolvedGroupId;

        if (query.GroupId.HasValue)
        {
            resolvedGroupId = query.GroupId.Value;
        }
        else if (query.ResourceSchemaId.HasValue)
        {
            var resourceSchema = await _dataProvider.GetResourceSchemaAsync(query.ResourceSchemaId.Value, cancellationToken);

            if (resourceSchema is null)
                throw new ResourcesDomainException("Resource schema not found.");

            resolvedGroupId = resourceSchema.GroupId;
        }
        else
        {
            throw new ResourcesDomainException("Either GroupId or ResourceSchemaId must be provided.");
        }

        var isOwner = await _dataProvider.IsOwnerAsync(resolvedGroupId, query.RequesterId, cancellationToken);
        var isMember = !isOwner && await _dataProvider.IsMemberAsync(resolvedGroupId, query.RequesterId, cancellationToken);

        if (!isOwner && !isMember)
            throw new ResourcesForbiddenException("Access to this group is forbidden.");

        var take = Math.Clamp(query.Take == 0 ? DefaultTake : query.Take, MinTake, MaxTake);

        var instances = await _dataProvider.GetInstancesAsync(
            query.ResourceSchemaId, query.GroupId, query.IncludeDeleted, query.AfterId, take, cancellationToken);

        if (instances.Count == 0)
            return new GetResourceInstancesQueryResult([], null);

        var relevantTypeIds = instances.Select(i => i.ResourceSchemaId).Distinct();
        var definitions = await _dataProvider.GetPropertyDefinitionsAsync(relevantTypeIds, cancellationToken);
        var defMap = definitions.ToDictionary(d => d.Id);

        var items = instances
            .Select(instance =>
            {
                var propertyValues = instance.PropertyValues
                    .Select(pv =>
                    {
                        defMap.TryGetValue(pv.PropertyDefinitionId, out var def);
                        return new PropertyValueResult(
                            pv.PropertyDefinitionId,
                            def?.Name ?? string.Empty,
                            def?.DataType.ToString() ?? string.Empty,
                            pv.Value);
                    })
                    .ToList();

                return new ResourceInstanceRowDto(
                    instance.Id,
                    instance.ResourceSchemaId,
                    instance.GroupId,
                    instance.Name,
                    instance.Description,
                    instance.OwnerId,
                    instance.CreatedAt,
                    instance.DeletedAt,
                    propertyValues);
            })
            .ToList();

        var nextCursor = items.Count == take ? items[^1].Id : (Guid?)null;

        return new GetResourceInstancesQueryResult(items, nextCursor);
    }
}
