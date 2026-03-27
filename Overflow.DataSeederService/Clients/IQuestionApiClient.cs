using Overflow.DataSeederService.Models;
using Refit;

namespace Overflow.DataSeederService.Clients;

[Headers("Content-Type: application/json")]
public interface IQuestionApiClient
{
    [Post("/questions/{questionId}/answers")]
    Task<Answer> CreateAnswerAsync(
        string questionId,
        [Body] CreateAnswerDto body,
        [Header("Authorization")] string authorization,
        CancellationToken ct = default);
}