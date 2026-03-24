using Overflow.QuestionService.Validators;
using Shouldly;

namespace Overflow.QuestionService.UnitTests.Validators;

public class TagListValidatorTests
{
    private readonly TagListValidator _sut = new(1, 5);

    [Fact]
    public void IsValid_WithinRange_ReturnsSuccess()
    {
        // Arrange
        var tags = new List<string> { "csharp", "aspnet" };

        // Act
        var result = _sut.GetValidationResult(tags, new System.ComponentModel.DataAnnotations.ValidationContext(tags));

        // Assert
        result.ShouldBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_EmptyList_ReturnsError()
    {
        // Arrange
        var tags = new List<string>();

        // Act
        var result = _sut.GetValidationResult(tags, new System.ComponentModel.DataAnnotations.ValidationContext(tags));

        // Assert
        result.ShouldNotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
        result!.ErrorMessage.ShouldContain("at least 1");
    }

    [Fact]
    public void IsValid_TooManyTags_ReturnsError()
    {
        // Arrange
        var tags = new List<string> { "a", "b", "c", "d", "e", "f" };

        // Act
        var result = _sut.GetValidationResult(tags, new System.ComponentModel.DataAnnotations.ValidationContext(tags));

        // Assert
        result.ShouldNotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_ExactlyOneTag_ReturnsSuccess()
    {
        // Arrange
        var tags = new List<string> { "single" };

        // Act
        var result = _sut.GetValidationResult(tags, new System.ComponentModel.DataAnnotations.ValidationContext(tags));

        // Assert
        result.ShouldBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_ExactlyFiveTags_ReturnsSuccess()
    {
        // Arrange
        var tags = new List<string> { "a", "b", "c", "d", "e" };

        // Act
        var result = _sut.GetValidationResult(tags, new System.ComponentModel.DataAnnotations.ValidationContext(tags));

        // Assert
        result.ShouldBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_NullValue_ReturnsError()
    {
        // Arrange & Act
        var result = _sut.GetValidationResult(null,
            new System.ComponentModel.DataAnnotations.ValidationContext(new object()));

        // Assert
        result.ShouldNotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }
}