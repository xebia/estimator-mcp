using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace CatalogEditor.Components.Account;

// Cookie expiry (7-day hard limit) is enforced by the auth middleware.
// This provider just holds the session auth state for active Blazor Server connections.
internal sealed class CookieAuthenticationStateProvider(ILoggerFactory loggerFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromHours(1);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
