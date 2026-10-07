using System;
using System.Collections.Generic;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchemas;

internal record GetResourceSchemasQuery(Guid GroupId, Guid RequesterId) : IQuery<IReadOnlyList<GetResourceSchemasQueryResult>>;
