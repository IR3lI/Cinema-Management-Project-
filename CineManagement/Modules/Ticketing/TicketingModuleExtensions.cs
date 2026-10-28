using Microsoft.Extensions.DependencyInjection;
using CineManagement.Modules.Ticketing.Services;

namespace CineManagement.Modules.Ticketing;

public static class TicketingModuleExtensions
{
    public static IServiceCollection AddTicketingModule(this IServiceCollection services)
    {
        services.AddScoped<ITicketingService, TicketingService>();
        services.AddHostedService<SeatHoldTimeoutWorker>();
        return services;
    }
}
