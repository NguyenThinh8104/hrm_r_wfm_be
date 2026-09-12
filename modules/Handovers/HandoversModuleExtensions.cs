using Microsoft.Extensions.DependencyInjection;
using Modules.Handovers.Interfaces;
using Modules.Handovers.Services;

namespace Modules.Handovers;

public static class HandoversModuleExtensions
{
    public static IServiceCollection AddHandoversModule(this IServiceCollection services)
    {
        services.AddScoped<IHandoverService, HandoverService>();
        return services;
    }
}

