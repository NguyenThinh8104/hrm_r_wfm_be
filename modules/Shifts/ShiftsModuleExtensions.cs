using Microsoft.Extensions.DependencyInjection;
using Modules.Shifts.Interfaces;
using Modules.Shifts.Services;

namespace Modules.Shifts;

public static class ShiftsModuleExtensions
{
    public static IServiceCollection AddShiftsModule(this IServiceCollection services)
    {
        services.AddScoped<IShiftService, ShiftService>();
        return services;
    }
}

