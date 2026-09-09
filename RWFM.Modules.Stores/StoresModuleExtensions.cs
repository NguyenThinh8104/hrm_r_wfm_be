using Microsoft.Extensions.DependencyInjection;

namespace RWFM.Modules.Stores;

public static class StoresModuleExtensions
{
    public static IServiceCollection AddStoresModule(this IServiceCollection services)
    {
        return services;
    }
}
