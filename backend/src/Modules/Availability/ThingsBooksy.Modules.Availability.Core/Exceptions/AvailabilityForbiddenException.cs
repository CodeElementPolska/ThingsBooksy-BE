using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Availability.Core.Exceptions;

internal sealed class AvailabilityForbiddenException : ForbiddenException
{
    public AvailabilityForbiddenException(string message) : base(message) { }
}
