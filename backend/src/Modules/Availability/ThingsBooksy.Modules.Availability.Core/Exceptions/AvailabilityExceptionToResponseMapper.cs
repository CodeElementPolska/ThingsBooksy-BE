using System.Net;
using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Availability.Core.Exceptions;

internal sealed class AvailabilityExceptionToResponseMapper : IExceptionToResponseMapper
{
    public ExceptionResponse Map(Exception exception)
        => exception switch
        {
            AvailabilityConflictException e => new ExceptionResponse(
                new { code = "AVAILABILITY_CONFLICT", message = e.Message },
                HttpStatusCode.BadRequest),
            AvailabilityForbiddenException e => new ExceptionResponse(
                new { code = "AVAILABILITY_FORBIDDEN", message = e.Message },
                HttpStatusCode.Forbidden),
            AvailabilityNotFoundException e => new ExceptionResponse(
                new { code = "AVAILABILITY_NOT_FOUND", message = e.Message },
                HttpStatusCode.NotFound),
            AvailabilityDomainException e => new ExceptionResponse(
                new { code = "AVAILABILITY_DOMAIN_ERROR", message = e.Message },
                HttpStatusCode.BadRequest),
            _ => null!
        };
}
