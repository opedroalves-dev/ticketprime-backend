using Microsoft.AspNetCore.Authentication;

namespace TicketPrime.Api.Authentication;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
