using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Availability.Core.Exceptions;

internal sealed class AvailabilityNotFoundException : NotFoundException
{
    public AvailabilityNotFoundException(string message) : base(message) { }
}
