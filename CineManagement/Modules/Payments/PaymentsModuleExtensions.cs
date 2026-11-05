using Microsoft.Extensions.DependencyInjection;
using CineManagement.Modules.Payments.Services;

namespace CineManagement.Modules.Payments;

public static class PaymentsModuleExtensions
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddHostedService<OutboxMessageWorker>();
        return services;
    }
}
