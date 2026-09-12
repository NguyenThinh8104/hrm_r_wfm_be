using Microsoft.Extensions.DependencyInjection;
using Modules.Auth.Interfaces;
using Modules.Auth.Services;

namespace Modules.Auth;

public static class AuthModuleExtensions
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

