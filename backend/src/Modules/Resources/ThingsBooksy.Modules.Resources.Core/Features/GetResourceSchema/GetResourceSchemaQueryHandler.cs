using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema;

internal sealed class GetResourceSchemaQueryHandler : IQueryHandler<GetResourceSchemaQuery, GetResourceSchemaQueryResult?>
{
    private readonly IGetResourceSchemaQueryDataProvider _dataProvider;

    public GetResourceSchemaQueryHandler(IGetResourceSchemaQueryDataProvider dataProvider)
        => _dataProvider = dataProvider;

    public async Task<GetResourceSchemaQueryResult?> HandleAsync(GetResourceSchemaQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _dataProvider.GetByIdAsync(query.SchemaId, cancellationToken);

        if (result is null)
            return null;

        var isOwner = await _dataProvider.IsOwnerAsync(result.GroupId, query.RequesterId, cancellationToken);
        var isMember = !isOwner && await _dataProvider.IsMemberAsync(result.GroupId, query.RequesterId, cancellationToken);

        if (!isOwner && !isMember)
            throw new ResourcesForbiddenException("Access to this group is forbidden.");

        return result;
    }
}
