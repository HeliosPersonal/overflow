using System.Net.Http.Headers;

namespace Overflow.DataSeederService.Clients;

public static class AdminTokenAccessor
{
    private static readonly AsyncLocal<string?> Token = new();

    public static string? Current
    {
        get => Token.Value;
        set => Token.Value = value;
    }
}

public class AdminBearerTokenHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = AdminTokenAccessor.Current;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}