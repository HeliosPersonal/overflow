using Overflow.DataSeederService.Models;
using Refit;

namespace Overflow.DataSeederService.Clients;

/// <summary>Vote Service REST API.</summary>
[Headers("Content-Type: application/json")]
public interface IVoteApiClient
{
    [Post("/votes")]
    Task<HttpResponseMessage> CastVoteAsync(
        [Body] CastVoteDto body,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default);
}