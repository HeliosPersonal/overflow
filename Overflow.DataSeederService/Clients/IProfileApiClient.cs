using Refit;

namespace Overflow.DataSeederService.Clients;

[Headers("Content-Type: application/json")]
public interface IProfileApiClient
{
    [Get("/profiles/me")]
    Task<HttpResponseMessage> GetMyProfileAsync(
        [Header("Authorization")] string authorization, CancellationToken ct = default);
}