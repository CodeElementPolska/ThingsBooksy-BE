using System;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceType;

internal interface IDeleteResourceTypeCommandDataProvider : IDataProvider
{
    Task<ResourceType?> GetResourceTypeAsync(Guid typeId, CancellationToken ct);
    Task<GroupReadModel?> GetGroupAsync(Guid groupId, CancellationToken ct);
    Task SoftDeleteInstancesAsync(Guid typeId, DateTime now, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
