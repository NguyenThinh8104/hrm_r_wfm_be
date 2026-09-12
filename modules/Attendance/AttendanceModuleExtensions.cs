using Microsoft.Extensions.DependencyInjection;
using Modules.Attendance.Interfaces;
using Modules.Attendance.Services;

namespace Modules.Attendance;

public static class AttendanceModuleExtensions
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}

