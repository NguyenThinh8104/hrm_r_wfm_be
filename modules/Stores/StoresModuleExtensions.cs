using Microsoft.Extensions.DependencyInjection;
using Modules.Stores.Interfaces;
using Modules.Stores.Services;

namespace Modules.Stores;

public static class StoresModuleExtensions
{
    public static IServiceCollection AddStoresModule(this IServiceCollection services)
    {
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IStoreService, BranchService>();
        services.AddScoped<IKioskService, KioskService>();
        services.AddScoped<BranchHeadcountService>();
        services.AddScoped<IStoreHeadcountService>(sp => sp.GetRequiredService<BranchHeadcountService>());
        services.AddScoped<Shared.Interfaces.IBranchHeadcountService>(sp => sp.GetRequiredService<BranchHeadcountService>());
        return services;
    }
}

