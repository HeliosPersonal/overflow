using Shouldly;

namespace Overflow.Contracts.UnitTests;

public class VoteTargetTypeTests
{
    [Theory]
    [InlineData("Question", true)]
    [InlineData("Answer", true)]
    [InlineData("Comment", false)]
    [InlineData("", false)]
    [InlineData("question", false)] // case-sensitive
    public void IsValid_ReturnsExpectedResult(string targetType, bool expected)
    {
        // Act
        var result = VoteTargetType.IsValid(targetType);

        // Assert
        result.ShouldBe(expected);
    }
}