using Microsoft.Extensions.DependencyInjection;
using RWFM.Modules.Auth.Interfaces;
using RWFM.Modules.Auth.Services;

namespace RWFM.Modules.Auth;

public static class AuthModuleExtensions
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
