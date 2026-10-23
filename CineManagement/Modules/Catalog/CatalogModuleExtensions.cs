using Microsoft.Extensions.DependencyInjection;
using CineManagement.Modules.Catalog.Services;

namespace CineManagement.Modules.Catalog;

public static class CatalogModuleExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<ICatalogService, CatalogService>();
        return services;
    }
}
