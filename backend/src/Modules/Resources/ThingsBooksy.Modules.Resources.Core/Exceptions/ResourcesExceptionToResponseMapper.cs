using System;
using System.Net;
using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Resources.Core.Exceptions;

internal sealed class ResourcesExceptionToResponseMapper : IExceptionToResponseMapper
{
    public ExceptionResponse Map(Exception exception)
        => exception switch
        {
            ResourceSchemaNameAlreadyExistsException => new ExceptionResponse(
                new { code = "RESOURCE_SCHEMA_NAME_TAKEN", message = "A schema with this name already exists in the group." },
                HttpStatusCode.Conflict),
            _ => null!
        };
}
