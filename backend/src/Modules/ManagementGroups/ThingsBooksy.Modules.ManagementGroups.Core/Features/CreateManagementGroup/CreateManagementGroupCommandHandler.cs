using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ThingsBooksy.Modules.ManagementGroups.Core.Domain;
using ThingsBooksy.Modules.ManagementGroups.Core.Exceptions;
using ThingsBooksy.Modules.ManagementGroups.Core.Features.CreateManagementGroup.DataProviders;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Abstractions.Time;

namespace ThingsBooksy.Modules.ManagementGroups.Core.Features.CreateManagementGroup;

internal sealed class CreateManagementGroupCommandHandler : ICommandHandler<CreateManagementGroupCommand>
{
    private const string CreatedGroupIdKey = "created_group_id";

    private readonly ICreateManagementGroupCommandDataProvider _provider;
    private readonly IClock _clock;
    private readonly IMessageBroker _messageBroker;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateManagementGroupCommandHandler(
        ICreateManagementGroupCommandDataProvider provider,
        IClock clock,
        IMessageBroker messageBroker,
        IHttpContextAccessor httpContextAccessor)
    {
        _provider = provider;
        _clock = clock;
        _messageBroker = messageBroker;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task HandleAsync(CreateManagementGroupCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ManagementGroupsDomainException("Group name cannot be empty.");

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(command.TimeZoneId, out _))
            throw new ManagementGroupsDomainException("Invalid timezone identifier.");

        var trimmedName = command.Name.Trim();

        if (await _provider.OwnerNameExistsAsync(command.OwnerId, trimmedName, cancellationToken))
            throw new GroupNameAlreadyTakenException(command.Name);

        var group = ManagementGroup.Create(command, _clock.CurrentDate());
        await _provider.AddAsync(group, cancellationToken);
        await _provider.SaveChangesAsync(cancellationToken);
        _httpContextAccessor.HttpContext?.Items.TryAdd(CreatedGroupIdKey, group.Id);
        await _messageBroker.PublishAsync(new GroupCreated(group.Id, group.OwnerId, group.TimeZoneId), cancellationToken);
    }
}
