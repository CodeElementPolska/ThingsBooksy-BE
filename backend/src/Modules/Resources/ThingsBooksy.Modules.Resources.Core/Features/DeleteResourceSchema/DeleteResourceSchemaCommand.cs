using System;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceSchema;

internal record DeleteResourceSchemaCommand(Guid SchemaId, Guid RequesterId) : ICommand;
