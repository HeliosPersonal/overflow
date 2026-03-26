using Overflow.DataSeederService.Models;
using Refit;

namespace Overflow.DataSeederService.Clients;

/// <summary>
///     Refit client for the Question Service REST API.
///     Used to post AI-generated answers.
/// </summary>
[Headers("Content-Type: application/json")]
public interface IQuestionApiClient
{
    [Post("/questions/{questionId}/answers")]
    Task<Answer> CreateAnswerAsync(
        string questionId,
        [Body] CreateAnswerDto body,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default);
}