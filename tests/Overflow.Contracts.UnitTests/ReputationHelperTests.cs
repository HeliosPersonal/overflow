using Overflow.Contracts.Helpers;
using Shouldly;

namespace Overflow.Contracts.UnitTests;

public class ReputationHelperTests
{
    [Fact]
    public void MakeEvent_QuestionUpvoted_ReturnsDeltaOf5()
    {
        // Arrange
        var userId = "user-1";
        var actorId = "actor-1";

        // Act
        var result = ReputationHelper.MakeEvent(userId, ReputationReason.QuestionUpvoted, actorId);

        // Assert
        result.UserId.ShouldBe(userId);
        result.ActorUserId.ShouldBe(actorId);
        result.Delta.ShouldBe(5);
        result.Reason.ShouldBe(ReputationReason.QuestionUpvoted);
    }

    [Fact]
    public void MakeEvent_AnswerUpvoted_ReturnsDeltaOf5()
    {
        // Arrange & Act
        var result = ReputationHelper.MakeEvent("u1", ReputationReason.AnswerUpvoted, "a1");

        // Assert
        result.Delta.ShouldBe(5);
    }

    [Fact]
    public void MakeEvent_QuestionDownvoted_ReturnsDeltaOfNegative2()
    {
        // Arrange & Act
        var result = ReputationHelper.MakeEvent("u1", ReputationReason.QuestionDownvoted, "a1");

        // Assert
        result.Delta.ShouldBe(-2);
    }

    [Fact]
    public void MakeEvent_AnswerDownvoted_ReturnsDeltaOfNegative2()
    {
        // Arrange & Act
        var result = ReputationHelper.MakeEvent("u1", ReputationReason.AnswerDownvoted, "a1");

        // Assert
        result.Delta.ShouldBe(-2);
    }

    [Fact]
    public void MakeEvent_AnswerAccepted_ReturnsDeltaOf15()
    {
        // Arrange & Act
        var result = ReputationHelper.MakeEvent("u1", ReputationReason.AnswerAccepted, "a1");

        // Assert
        result.Delta.ShouldBe(15);
    }

    [Fact]
    public void MakeEvent_SetsOccurredToRecentTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = ReputationHelper.MakeEvent("u1", ReputationReason.QuestionUpvoted, "a1");

        // Assert
        result.Occurred.ShouldBeGreaterThanOrEqualTo(before);
        (result.Occurred - DateTime.UtcNow).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }
}