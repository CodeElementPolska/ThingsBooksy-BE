using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Availability.Core.Exceptions;

internal sealed class AvailabilityConflictException : CustomException
{
    public AvailabilityConflictException(string message) : base(message) { }
}
