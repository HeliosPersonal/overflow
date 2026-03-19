using System.Net.Http.Headers;

namespace Overflow.DataSeederService.Clients;

/// <summary>AsyncLocal store for the admin Bearer token injected by <see cref="AdminBearerTokenHandler" />.</summary>
public static class AdminTokenAccessor
{
    private static readonly AsyncLocal<string?> Token = new();

    public static string? Current
    {
        get => Token.Value;
        set => Token.Value = value;
    }
}

/// <summary>Injects the admin Bearer token from <see cref="AdminTokenAccessor" /> into every IKeycloakAdminClient request.</summary>
public class AdminBearerTokenHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = AdminTokenAccessor.Current;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}