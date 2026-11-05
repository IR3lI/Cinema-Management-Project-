using Microsoft.Extensions.DependencyInjection;
using CineManagement.Modules.Cinemas.Services;

namespace CineManagement.Modules.Cinemas;

public static class CinemasModuleExtensions
{
    public static IServiceCollection AddCinemasModule(this IServiceCollection services)
    {
        services.AddScoped<ISeatLayoutService, SeatLayoutService>();
        return services;
    }
}
