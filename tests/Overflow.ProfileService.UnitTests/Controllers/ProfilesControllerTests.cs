using System.Security.Claims;
using CommandFlow;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Overflow.ProfileService.Controllers;
using Overflow.ProfileService.DTOs;
using Overflow.ProfileService.Features.Profiles.Commands;
using Overflow.ProfileService.Features.Profiles.Queries;
using Shouldly;

namespace Overflow.ProfileService.UnitTests.Controllers;

public class ProfilesControllerTests
{
    private readonly Mock<ISender> _sender;
    private readonly ProfilesController _sut;

    public ProfilesControllerTests()
    {
        _sender = new Mock<ISender>();
        _sut = new ProfilesController(_sender.Object);
    }

    private void SetUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetProfile_Exists_ReturnsOk()
    {
        // Arrange
        var dto = new ProfileDto("u-1", "John", "Dev", null, 100, DateTime.UtcNow);
        _sender.Setup(s => s.Send(It.IsAny<GetProfileByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.GetProfile("u-1");

        // Assert
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ((ProfileDto)ok.Value!).UserId.ShouldBe("u-1");
    }

    [Fact]
    public async Task GetProfile_NotFound_Returns404()
    {
        // Arrange
        _sender.Setup(s => s.Send(It.IsAny<GetProfileByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileDto?)null);

        // Act
        var result = await _sut.GetProfile("nonexistent");

        // Assert
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task EditProfile_Success_ReturnsNoContent()
    {
        // Arrange
        SetUser("user-1");
        _sender.Setup(s => s.Send(It.IsAny<EditProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.EditProfile(new EditProfileDto("New Name", null, null));

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task EditProfile_NoUser_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        var result = await _sut.EditProfile(new EditProfileDto("Name", null, null));

        // Assert
        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMe_Authenticated_ReturnsProfile()
    {
        // Arrange
        SetUser("user-1");
        var dto = new ProfileDto("user-1", "Me", null, null, 0, DateTime.UtcNow);
        _sender.Setup(s => s.Send(It.IsAny<GetProfileByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.GetMe();

        // Assert
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ((ProfileDto)ok.Value!).UserId.ShouldBe("user-1");
    }

    [Fact]
    public async Task GetBatch_SplitsIdsAndReturnsResults()
    {
        // Arrange
        var summaries = new List<ProfileSummaryDto>
        {
            new("u-1", "One", 10, null),
            new("u-2", "Two", 20, null)
        };
        _sender.Setup(s => s.Send(It.IsAny<GetProfileBatchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        // Act
        var result = await _sut.GetBatch("u-1,u-2");

        // Assert
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ((List<ProfileSummaryDto>)ok.Value!).Count.ShouldBe(2);
    }
}