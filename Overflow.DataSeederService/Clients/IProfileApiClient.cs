using Refit;

namespace Overflow.DataSeederService.Clients;

/// <summary>Profile Service REST API. Used to trigger profile auto-creation for the AI user.</summary>
[Headers("Content-Type: application/json")]
public interface IProfileApiClient
{
    /// <summary>GET /profiles/me triggers UserProfileCreationMiddleware if the profile doesn't exist yet.</summary>
    [Get("/profiles/me")]
    Task<HttpResponseMessage> GetMyProfileAsync(
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default);
}