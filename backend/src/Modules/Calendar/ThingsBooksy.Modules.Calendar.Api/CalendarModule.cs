using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Calendar.Core;
using ThingsBooksy.Shared.Abstractions.Modules;

namespace ThingsBooksy.Modules.Calendar.Api;

internal sealed class CalendarModule : IModule
{
    public string Name { get; } = "Calendar";
    public IEnumerable<string> Policies { get; } = ["calendar"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddCalendarCore(configuration);
    }

    public void Use(IApplicationBuilder app) { }

    public void Expose(IEndpointRouteBuilder endpoints) { }
}
