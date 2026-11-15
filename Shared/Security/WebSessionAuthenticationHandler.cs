using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Data;

namespace Shared.Security;

public sealed class WebSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    WebSession sessions,
    AppDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var session = await sessions.ReadAsync(Context);
        if (session == null)
            return AuthenticateResult.NoResult();

        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == session.UserId, Context.RequestAborted);

        if (user == null || user.Status != "ACTIVE" || session != WebSessionIdentity.FromUser(user))
        {
            await sessions.EndAsync(Context);
            return AuthenticateResult.Fail("Session is no longer valid.");
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(JwtTokenService.CreateClaims(user), Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
