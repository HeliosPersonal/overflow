using Overflow.Common;
using Overflow.DataSeederService.Models;
using Refit;

namespace Overflow.DataSeederService.Clients;

/// <summary>
///     Refit client for the Question Service REST API.
///     Pass the token as <c>"Bearer {token}"</c> to the <c>authorization</c> parameter.
/// </summary>
[Headers("Content-Type: application/json")]
public interface IQuestionApiClient
{
    [Get("/questions")]
    Task<PaginationResult<Question>> GetQuestionsAsync(
        [Query] string sort = "newest",
        [Query] int page = 1,
        [Query] int pageSize = 20,
        CancellationToken cancellationToken = default);

    [Get("/questions/{id}")]
    Task<Question> GetQuestionByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    [Post("/questions")]
    Task<Question> CreateQuestionAsync(
        [Body] CreateQuestionDto body,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default);

    [Post("/questions/{questionId}/answers")]
    Task<Answer> CreateAnswerAsync(
        string questionId,
        [Body] CreateAnswerDto body,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default);

    [Post("/questions/{questionId}/answers/{answerId}/accept")]
    Task AcceptAnswerAsync(
        string questionId,
        string answerId,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default);

    [Get("/tags")]
    Task<List<Tag>> GetTagsAsync(
        CancellationToken cancellationToken = default);
}