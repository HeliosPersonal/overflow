using System.ComponentModel.DataAnnotations;

namespace Overflow.QuestionService.DTOs;

public record CreateTagDto(
    [Required][MaxLength(50)] string Name,
    [Required][MaxLength(50)] string Slug,
    [Required][MaxLength(1000)] string Description);

public record UpdateTagDto(
    [Required][MaxLength(50)] string Name,
    [Required][MaxLength(1000)] string Description);

