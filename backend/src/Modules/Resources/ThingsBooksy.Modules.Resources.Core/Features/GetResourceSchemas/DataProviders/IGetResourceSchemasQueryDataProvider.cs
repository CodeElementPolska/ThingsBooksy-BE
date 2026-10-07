using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchemas;

internal interface IGetResourceSchemasQueryDataProvider : IDataProvider
{
    Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<List<GetResourceSchemasQueryResult>> GetByGroupIdAsync(Guid groupId, CancellationToken ct);
}
