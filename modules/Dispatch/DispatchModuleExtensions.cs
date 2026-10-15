using Microsoft.Extensions.DependencyInjection;
using Modules.Dispatch.Interfaces;
using Modules.Dispatch.Services;

namespace Modules.Dispatch;

public static class DispatchModuleExtensions
{
    public static IServiceCollection AddDispatchModule(this IServiceCollection services)
    {
        services.AddScoped<IDispatchQuotaService, DispatchQuotaService>();
        services.AddScoped<IDispatchMetricsService, DispatchMetricsService>();
        services.AddScoped<IDispatchWorkflowService, DispatchWorkflowService>();
        services.AddScoped<IDispatchService, DispatchService>();
        services.AddScoped<IDispatchSyncService, DispatchSyncService>();
        services.AddHostedService<DispatchExpirationWorker>();
        return services;
    }
}
