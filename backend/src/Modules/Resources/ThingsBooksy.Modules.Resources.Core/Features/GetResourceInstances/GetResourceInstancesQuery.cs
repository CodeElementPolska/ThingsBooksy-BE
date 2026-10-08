using System;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstances;

internal record GetResourceInstancesQuery(
    Guid? ResourceSchemaId,
    Guid? GroupId,
    bool IncludeDeleted,
    Guid RequesterId,
    Guid? AfterId,
    int Take) : IQuery<GetResourceInstancesQueryResult>;
