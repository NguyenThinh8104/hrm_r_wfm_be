using Microsoft.Extensions.DependencyInjection;
using Modules.Dispatch.Interfaces;
using Modules.Dispatch.Services;

namespace Modules.Dispatch;

public static class DispatchModuleExtensions
{
    public static IServiceCollection AddDispatchModule(this IServiceCollection services)
    {
        services.AddScoped<IDispatchService, DispatchService>();
        return services;
    }
}

