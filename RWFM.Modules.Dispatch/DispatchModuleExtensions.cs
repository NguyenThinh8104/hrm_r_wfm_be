using Microsoft.Extensions.DependencyInjection;
using RWFM.Modules.Dispatch.Interfaces;
using RWFM.Modules.Dispatch.Services;

namespace RWFM.Modules.Dispatch;

public static class DispatchModuleExtensions
{
    public static IServiceCollection AddDispatchModule(this IServiceCollection services)
    {
        services.AddScoped<IDispatchService, DispatchService>();
        return services;
    }
}
