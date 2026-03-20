namespace Overflow.Contracts.Helpers;

public static class ReputationHelper
{
    private const int UpvoteDelta = 5;
    private const int DownvoteDelta = -2;
    private const int AnswerAcceptedDelta = 15;

    private static int GetDelta(ReputationReason reason) => reason switch
    {
        ReputationReason.QuestionUpvoted => UpvoteDelta,
        ReputationReason.QuestionDownvoted => DownvoteDelta,
        ReputationReason.AnswerUpvoted => UpvoteDelta,
        ReputationReason.AnswerDownvoted => DownvoteDelta,
        ReputationReason.AnswerAccepted => AnswerAcceptedDelta,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown reputation reason")
    };

    public static UserReputationChanged MakeEvent(string userId, ReputationReason reason, string actorUserId) =>
        new(
            UserId: userId,
            Delta: GetDelta(reason),
            Reason: reason,
            ActorUserId: actorUserId,
            Occurred: DateTime.UtcNow
        );
}