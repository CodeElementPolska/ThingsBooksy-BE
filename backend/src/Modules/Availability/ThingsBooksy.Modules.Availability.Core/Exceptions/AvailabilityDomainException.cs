using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Availability.Core.Exceptions;

internal sealed class AvailabilityDomainException : CustomException
{
    public AvailabilityDomainException(string message) : base(message) { }
}
