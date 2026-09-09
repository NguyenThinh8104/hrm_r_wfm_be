using Microsoft.Extensions.DependencyInjection;
using RWFM.Modules.Handovers.Interfaces;
using RWFM.Modules.Handovers.Services;

namespace RWFM.Modules.Handovers;

public static class HandoversModuleExtensions
{
    public static IServiceCollection AddHandoversModule(this IServiceCollection services)
    {
        services.AddScoped<IHandoverService, HandoverService>();
        return services;
    }
}
