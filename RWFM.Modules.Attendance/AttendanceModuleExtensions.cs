using Microsoft.Extensions.DependencyInjection;
using RWFM.Modules.Attendance.Interfaces;
using RWFM.Modules.Attendance.Services;

namespace RWFM.Modules.Attendance;

public static class AttendanceModuleExtensions
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}
