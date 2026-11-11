using Microsoft.Extensions.DependencyInjection;
using CineManagement.Modules.Ordering.Services;

namespace CineManagement.Modules.Ordering;

public static class OrderingModuleExtensions
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
