using Microsoft.Extensions.DependencyInjection;
using RWFM.Modules.Shifts.Interfaces;
using RWFM.Modules.Shifts.Services;

namespace RWFM.Modules.Shifts;

public static class ShiftsModuleExtensions
{
    public static IServiceCollection AddShiftsModule(this IServiceCollection services)
    {
        services.AddScoped<IShiftService, ShiftService>();
        return services;
    }
}
