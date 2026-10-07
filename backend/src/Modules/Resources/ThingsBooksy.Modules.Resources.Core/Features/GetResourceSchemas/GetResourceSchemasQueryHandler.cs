using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchemas;

internal sealed class GetResourceSchemasQueryHandler : IQueryHandler<GetResourceSchemasQuery, IReadOnlyList<GetResourceSchemasQueryResult>>
{
    private readonly IGetResourceSchemasQueryDataProvider _dataProvider;

    public GetResourceSchemasQueryHandler(IGetResourceSchemasQueryDataProvider dataProvider)
        => _dataProvider = dataProvider;

    public async Task<IReadOnlyList<GetResourceSchemasQueryResult>> HandleAsync(GetResourceSchemasQuery query, CancellationToken cancellationToken = default)
    {
        var isOwner = await _dataProvider.IsOwnerAsync(query.GroupId, query.RequesterId, cancellationToken);
        var isMember = !isOwner && await _dataProvider.IsMemberAsync(query.GroupId, query.RequesterId, cancellationToken);

        if (!isOwner && !isMember)
            throw new ResourcesForbiddenException("Access to this group is forbidden.");

        return await _dataProvider.GetByGroupIdAsync(query.GroupId, cancellationToken);
    }
}
