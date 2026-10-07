using System;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema;

internal record GetResourceSchemaQuery(Guid SchemaId, Guid RequesterId) : IQuery<GetResourceSchemaQueryResult?>;
