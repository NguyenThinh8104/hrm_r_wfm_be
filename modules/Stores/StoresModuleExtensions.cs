using Microsoft.Extensions.DependencyInjection;
using Modules.Stores.Interfaces;
using Modules.Stores.Services;

namespace Modules.Stores;

public static class StoresModuleExtensions
{
    public static IServiceCollection AddStoresModule(this IServiceCollection services)
    {
        services.AddScoped<IKioskService, KioskService>();
        return services;
    }
}

