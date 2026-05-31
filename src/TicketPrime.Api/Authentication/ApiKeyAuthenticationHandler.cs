using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TicketPrime.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            Logger.LogWarning(
                "Tentativa de acesso a endpoint admin sem header X-Api-Key. " +
                "Path: {Path}, IP: {RemoteIp}",
                Request.Path, Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("API Key não fornecida."));
        }

        var configuredKey = Options.ApiKey;
        if (apiKeyHeader != configuredKey)
        {
            Logger.LogWarning(
                "Tentativa de acesso a endpoint admin com API Key inválida. " +
                "Path: {Path}, IP: {RemoteIp}",
                Request.Path, Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("API Key inválida."));
        }

        Logger.LogInformation("Acesso autorizado a endpoint admin. Path: {Path}", Request.Path);

        var claims = new[] { new Claim(ClaimTypes.Name, "Admin") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
